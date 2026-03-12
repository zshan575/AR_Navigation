using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavigation.Core;
using ARNavigation.Data;

namespace ARNavigation.UI
{
    /// <summary>
    /// Drives all HUD panels based on RouteManager state changes.
    /// Updated for image-tracking flow (no QR scanner UI).
    ///
    /// PANELS:
    ///   ScanPanel     - shown while WaitingForImage
    ///   NavPanel      - shown while Navigating
    ///   CompletePanel - shown on RouteComplete
    /// </summary>
    public class NavigationUIController : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject ScanPanel;
        public GameObject NavPanel;
        public GameObject CompletePanel;

        [Header("Scan Panel")]
        [Tooltip("Main instruction. e.g. 'Point camera at the marker to begin.'")]
        public TextMeshProUGUI ScanInstructionText;
        public TextMeshProUGUI StepCounterText;

        [Header("Scan Panel — Viewfinder (optional)")]
        [Tooltip("A frame/overlay image shown while scanning. Purely decorative.")]
        public GameObject ViewfinderOverlay;

        [Header("Nav Panel")]
        public TextMeshProUGUI DestinationNameText;
        public TextMeshProUGUI DirectionLabelText;
        public TextMeshProUGUI DistanceText;
        public TextMeshProUGUI InstructionText;
        public Button          ArrivedButton;

        [Header("Progress Dots (optional)")]
        public Transform  ProgressDotsParent;
        public GameObject DotPrefab;
        public Color DotCompletedColor = new Color(0f, 0.96f, 0.83f, 1f);
        public Color DotActiveColor    = Color.white;
        public Color DotInactiveColor  = new Color(1f, 1f, 1f, 0.2f);

        [Header("Wrong Image Toast")]
        public GameObject      ToastPanel;
        public TextMeshProUGUI ToastText;
        float _toastTimer;

        Image[] _dots;

        void Start()
        {
            if (RouteManager.Instance != null)
            {
                RouteManager.Instance.OnStateChanged      += HandleStateChange;
                RouteManager.Instance.OnWrongImageDetected += HandleWrongImage;
            }

            ArrivedButton?.onClick.AddListener(() => RouteManager.Instance?.ConfirmArrival());

            BuildDots();

            SetActive(ScanPanel,     false);
            SetActive(NavPanel,      false);
            SetActive(CompletePanel, false);
            SetActive(ToastPanel,    false);
        }

        void OnDestroy()
        {
            if (RouteManager.Instance != null)
            {
                RouteManager.Instance.OnStateChanged       -= HandleStateChange;
                RouteManager.Instance.OnWrongImageDetected -= HandleWrongImage;
            }
        }

        void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f) SetActive(ToastPanel, false);
            }
        }

        // ── State Handling ────────────────────────────────────────────────────────
        void HandleStateChange(RouteManager.NavState state, NavigationStop stop)
        {
            SetActive(ScanPanel,     false);
            SetActive(NavPanel,      false);
            SetActive(CompletePanel, false);

            var rm = RouteManager.Instance;

            switch (state)
            {
                case RouteManager.NavState.WaitingForImage:
                    SetActive(ScanPanel, true);

                    if (ScanInstructionText != null)
                        ScanInstructionText.text = rm.CurrentStepIndex == 0
                            ? "Point the camera at the location marker to begin."
                            : $"Find and point the camera at the marker for:\n<b>{stop?.DisplayName}</b>";

                    if (StepCounterText != null)
                        StepCounterText.text = $"STEP {rm.CurrentStepIndex + 1} OF {rm.TotalSteps}";

                    SetActive(ViewfinderOverlay, true);
                    break;

                case RouteManager.NavState.Navigating:
                    SetActive(NavPanel, true);
                    SetActive(ViewfinderOverlay, false);
                    PopulateNavPanel(stop);
                    UpdateDots(rm.CurrentStepIndex);
                    break;

                case RouteManager.NavState.RouteComplete:
                    SetActive(CompletePanel, true);
                    UpdateDots(rm.TotalSteps);
                    break;
            }
        }

        void PopulateNavPanel(NavigationStop stop)
        {
            if (stop == null) return;
            if (DestinationNameText != null) DestinationNameText.text = stop.DisplayName;
            if (InstructionText     != null) InstructionText.text     = stop.Instruction;
            if (DistanceText        != null) DistanceText.text        = $"~{stop.DistanceMetres:0} m";
            if (DirectionLabelText  != null) DirectionLabelText.text  = BearingToLabel(stop.BearingDegrees);
        }

        // ── Progress Dots ─────────────────────────────────────────────────────────
        void BuildDots()
        {
            if (ProgressDotsParent == null || DotPrefab == null || RouteManager.Instance == null) return;
            foreach (Transform c in ProgressDotsParent) Destroy(c.gameObject);
            int count = RouteManager.Instance.TotalSteps;
            _dots = new Image[count];
            for (int i = 0; i < count; i++)
                _dots[i] = Instantiate(DotPrefab, ProgressDotsParent).GetComponent<Image>();
        }

        void UpdateDots(int activeIndex)
        {
            if (_dots == null) return;
            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;
                _dots[i].color = i < activeIndex ? DotCompletedColor
                               : i == activeIndex ? DotActiveColor
                               : DotInactiveColor;
            }
        }

        // ── Wrong Image Toast ─────────────────────────────────────────────────────
        void HandleWrongImage(string seenName)
        {
            if (ToastPanel == null) return;
            SetActive(ToastPanel, true);
            if (ToastText != null)
                ToastText.text = $"Wrong marker! Keep looking for:\n<b>{RouteManager.Instance?.CurrentStop?.ImageName}</b>";
            _toastTimer = 3f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        static void SetActive(GameObject go, bool v) { if (go != null) go.SetActive(v); }

        static string BearingToLabel(float deg)
        {
            deg = ((deg % 360f) + 360f) % 360f;
            if (deg < 22.5f  || deg >= 337.5f) return "STRAIGHT AHEAD";
            if (deg < 67.5f)  return "BEAR RIGHT";
            if (deg < 112.5f) return "TURN RIGHT";
            if (deg < 157.5f) return "TURN BACK-RIGHT";
            if (deg < 202.5f) return "U-TURN";
            if (deg < 247.5f) return "TURN BACK-LEFT";
            if (deg < 292.5f) return "TURN LEFT";
            return "BEAR LEFT";
        }
    }
}
