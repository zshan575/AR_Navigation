using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavigation.Core;
using ARNavigation.Data;
using ARNavigation.ImageTracking;

namespace ARNavigation.Navigation
{
    [RequireComponent(typeof(LineRenderer))]
    public class ARPathLineRenderer : MonoBehaviour
    {
        [Header("AR Camera")]
        public Camera ARCamera;
        public Material PathMaterial;

        [Header("UI Panels")]
        public GameObject HomePanel;
        public GameObject ScanPanel;
        public GameObject NavigatePanel;
        public GameObject ArrivedPanel;
        public GameObject CompletePanel;

        [Header("Home Panel")]
        public Button          StartButton;
        public TextMeshProUGUI AppTitleText;
        public TextMeshProUGUI RouteDescriptionText;

        [Header("Scan Panel")]
        public TextMeshProUGUI ScanStepText;
        public TextMeshProUGUI ScanInstructionText;
        public TextMeshProUGUI ScanTargetText;

        [Header("Navigate Panel")]
        public TextMeshProUGUI NavDestinationText;
        public TextMeshProUGUI NavStepText;
        public TextMeshProUGUI NavDistanceText;
        public TextMeshProUGUI NavInstructionText;

        [Header("Arrived Panel")]
        public TextMeshProUGUI ArrivedTitleText;
        public TextMeshProUGUI ArrivedStopNameText;
        public Button          NextLocationButton;
        public TextMeshProUGUI NextLocationButtonText;

        [Header("Complete Panel")]
        public TextMeshProUGUI CompleteTitleText;
        public TextMeshProUGUI CompleteSubText;
        public Button          RestartButton;

        [Header("Line")]
        [Range(4,40)]  public int   PointCount   = 20;
        [Range(0f,1f)] public float GroundOffset = 0.05f;
        public float StartWidth = 0.10f;
        public float EndWidth   = 0.03f;
        public Color LineColor  = new Color(0f, 0.86f, 0.78f, 0.9f);
        [Range(0f,3f)] public float ScrollSpeed = 0.7f;

        [Header("Pin")]
        public GameObject PinObject;
        public Color PinColor  = new Color(0f, 0.95f, 0.8f, 1f);
        public float PinHeight = 0f;

        [Header("Arrival")]
        [Range(0.3f,5f)] public float ArrivalRadius = 1.5f;

        [Header("Delay after scan (seconds)")]
        [Range(0f,5f)] public float NavigationStartDelay = 1f;

        [Header("Debug")]
        public bool ForceShowLine  = false;
        public bool VerboseLogging = true;

        // ─── private ──────────────────────────────────────────
        LineRenderer _lr;
        Material     _mat;
        RouteManager _rm;
        Coroutine    _delayCo;

        Vector3 _destWorld;
        bool    _destLocked;
        bool    _lineActive;
        bool    _arrivedShown;

        float _alpha, _scroll, _bobTimer, _ringTimer, _logTimer;

        GameObject _pinRoot, _pinRing;

        public Vector3 DestinationWorld  => _destWorld;
        public bool    DestinationLocked => _destLocked;

        // ─── lifecycle ────────────────────────────────────────
        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            BuildMaterial();
            SetupLR();
        }

        void Start()
        {
            _rm = RouteManager.Instance;

            if (StartButton        != null) StartButton.onClick.AddListener(OnStartPressed);
            if (NextLocationButton != null) NextLocationButton.onClick.AddListener(OnNextLocationPressed);
            if (RestartButton      != null) RestartButton.onClick.AddListener(OnRestartPressed);

            if (_rm != null) _rm.OnStateChanged += OnState;

            // Start on home screen
            ShowScreen(Screen.Home);
        }

        void OnDestroy()
        {
            if (_rm  != null) _rm.OnStateChanged -= OnState;
            if (_mat != null) Destroy(_mat);
        }

        // ─── update ───────────────────────────────────────────
        void Update()
        {
            if (ARCamera == null) { ARCamera = Camera.main; if (ARCamera == null) return; }

            if (ForceShowLine && !_destLocked) LockDest(5f);

            _alpha = Mathf.MoveTowards(_alpha,
                (_lineActive || ForceShowLine) ? 1f : 0f,
                Time.deltaTime / 0.35f);

            if (_alpha < 0.01f)
            {
                _lr.enabled = false;
                if (_pinRoot != null) _pinRoot.SetActive(false);
                return;
            }

            _lr.enabled = true;
            if (_pinRoot != null) _pinRoot.SetActive(true);

            if (_destLocked)
            {
                DrawLine();
                CheckArrival();
                UpdateDist();
            }

            AnimateLine();
            AnimatePin();

            if (VerboseLogging)
            {
                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = 1f;
                    Debug.Log($"[PathLine] lineActive={_lineActive} locked={_destLocked} alpha={_alpha:F2}");
                }
            }
        }

        // ─── screen enum ──────────────────────────────────────
        enum Screen { Home, Scan, Navigate, Arrived, Complete }

        // THE ONE function that controls panels
        // Every panel is explicitly set — no ambiguity
        void ShowScreen(Screen s)
        {
            if (HomePanel     != null) HomePanel.SetActive(s == Screen.Home);
            if (ScanPanel     != null) ScanPanel.SetActive(s == Screen.Scan);
            if (NavigatePanel != null) NavigatePanel.SetActive(s == Screen.Navigate);
            if (ArrivedPanel  != null) ArrivedPanel.SetActive(s == Screen.Arrived);
            if (CompletePanel != null) CompletePanel.SetActive(s == Screen.Complete);
            Debug.Log($"[PathLine] ShowScreen({s}) → " +
                      $"Home={s==Screen.Home} Scan={s==Screen.Scan} " +
                      $"Nav={s==Screen.Navigate||s==Screen.Arrived} " +
                      $"Arrived={s==Screen.Arrived} Complete={s==Screen.Complete}");
        }

        // ─── route state handler ──────────────────────────────
        void OnState(RouteManager.NavState state, NavigationStop stop)
        {
            Debug.Log($"[PathLine] RouteManager → {state}");

            // Cancel any running countdown
            if (_delayCo != null) { StopCoroutine(_delayCo); _delayCo = null; }

            switch (state)
            {
                // Image not yet scanned — show scan screen, clear line+pin
                case RouteManager.NavState.WaitingForImage:
                    KillLine();
                    FillScanUI();
                    ShowScreen(Screen.Scan);
                    
                    break;

                // Image just scanned — start countdown then navigate
                case RouteManager.NavState.Navigating:
                     CountdownThenNavigate(stop);
                    break;

                case RouteManager.NavState.RouteComplete:
                    KillLine();
                    FillCompleteUI();
                    ShowScreen(Screen.Complete);
                    break;

                case RouteManager.NavState.Idle:
                    KillLine();
                    FillHomeUI();
                    ShowScreen(Screen.Home);
                    break;
            }
        }

        // Countdown on scan panel → then switch to navigate
        void CountdownThenNavigate(NavigationStop stop)
        {
            LockDest(stop?.DistanceMetres > 0 ? stop.DistanceMetres : 5f);
            _lineActive = true;
            FillNavigateUI(stop);
            ShowScreen(Screen.Navigate);  // ← ScanPanel turns OFF here
            ScanPanel.SetActive(false);
            NavigatePanel.SetActive(true);
        }

        // ─── fill UI helpers ──────────────────────────────────
        void FillHomeUI()
        {
            if (AppTitleText         != null) AppTitleText.text = "AR Navigator";
            if (RouteDescriptionText != null && _rm != null)
                RouteDescriptionText.text = $"{_rm.Route.RouteName}  •  {_rm.TotalSteps} stops";
        }

        void FillScanUI()
        {
            if (_rm == null) return;
            int step = _rm.CurrentStepIndex + 1;
            if (ScanStepText        != null) ScanStepText.text        = $"STEP {step} OF {_rm.TotalSteps}";
            if (ScanInstructionText != null) ScanInstructionText.text = "Point camera at\nthe location marker";
            if (ScanTargetText      != null) ScanTargetText.text      = _rm.CurrentStop != null
                ? $"Looking for:\n<b>{_rm.CurrentStop.DisplayName}</b>" : "";
        }

        void FillNavigateUI(NavigationStop stop)
        {
            _arrivedShown = false;
            if (stop == null) return;
            if (NavDestinationText != null) NavDestinationText.text = stop.DisplayName;
            if (NavInstructionText != null) NavInstructionText.text = stop.Instruction;
            if (NavStepText != null && _rm != null)
                NavStepText.text = $"Stop {_rm.CurrentStepIndex + 1} of {_rm.TotalSteps}";
        }

        void FillArrivedUI(NavigationStop stop)
        {
            if (ArrivedTitleText    != null) ArrivedTitleText.text    = "You Have Arrived!";
            if (ArrivedStopNameText != null) ArrivedStopNameText.text = stop?.DisplayName ?? "";
            bool last = _rm != null && _rm.CurrentStepIndex >= _rm.TotalSteps - 1;
            if (NextLocationButtonText != null)
                NextLocationButtonText.text = last ? "Finish" : "Next Location";
        }

        void FillCompleteUI()
        {
            if (CompleteTitleText != null) CompleteTitleText.text = "Route Complete!";
            if (CompleteSubText   != null && _rm != null)
                CompleteSubText.text = $"You visited all {_rm.TotalSteps} locations!";
        }

        // ─── buttons ──────────────────────────────────────────
        void OnStartPressed()
        {
            Debug.Log("[PathLine] Start pressed");
            FillHomeUI();
            ARImageTracker.Instance.OnStartButton();
            ScanPanel.SetActive(true); 
            _rm?.StartNavigation();
            // → RouteManager fires WaitingForImage → OnState → ShowScreen(Scan)
        }

        void OnNextLocationPressed()
        {
            Debug.Log("[PathLine] Next Location pressed");

            // Kill line and pin IMMEDIATELY
            KillLine();
            ArrivedPanel.SetActive(false);
            // Advance route — this fires OnState
            // If more stops  → WaitingForImage → ShowScreen(Scan)
            // If last stop   → RouteComplete   → ShowScreen(Complete)
            _rm?.ConfirmArrival();
        }

        void OnRestartPressed()
        {
            Debug.Log("[PathLine] Restart pressed");
            KillLine();
            _rm?.ResetRoute();
            FillHomeUI();
            ShowScreen(Screen.Home);
        }

        // ─── arrival ──────────────────────────────────────────
        void CheckArrival()
        {
            if (_arrivedShown) return;
            float dist = FlatDist(ARCamera.transform.position, _destWorld);
            if (dist <= ArrivalRadius)
            {
                _arrivedShown = true;
                FillArrivedUI(_rm?.CurrentStop);
                ShowScreen(Screen.Arrived);  // NavigatePanel stays ON, ArrivedPanel shows on top
                Debug.Log($"[PathLine] ARRIVED! dist={dist:F2}m");
            }
        }

        void UpdateDist()
        {
            if (NavDistanceText == null) return;
            float d = FlatDist(ARCamera.transform.position, _destWorld);
            NavDistanceText.text = d > 1f ? $"{d:F1} m away" : "Almost there!";
        }

        // ─── kill line + pin ──────────────────────────────────
        void KillLine()
        {
            _lineActive   = false;
            _destLocked   = false;
            _arrivedShown = false;
            _alpha        = 0f;
            _lr.enabled   = false;
            DestroyPin();
            Debug.Log("[PathLine] Line + pin killed");
        }

        public void ClearDestination() => KillLine();

        // ─── destination ──────────────────────────────────────
        void LockDest(float metres)
        {
            Vector3 fwd = ARCamera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            float gy   = GroundY();
            _destWorld = new Vector3(
                ARCamera.transform.position.x + fwd.x * metres, gy,
                ARCamera.transform.position.z + fwd.z * metres);
            _destLocked   = true;
            _arrivedShown = false;
            _bobTimer = _ringTimer = 0f;
            BuildPin();
            Debug.Log($"[PathLine] Dest locked {metres}m → {_destWorld}");
        }

        void DrawLine()
        {
            float gy  = GroundY() + GroundOffset;
            Vector3 s = new Vector3(ARCamera.transform.position.x, gy, ARCamera.transform.position.z);
            Vector3 e = new Vector3(_destWorld.x, gy, _destWorld.z);
            for (int i = 0; i < PointCount; i++)
                _lr.SetPosition(i, Vector3.Lerp(s, e, (float)i / (PointCount - 1)));
        }

        // ─── pin ──────────────────────────────────────────────
        void BuildPin()
        {
           // DestroyPin();
        /*    _pinRoot = new GameObject("DestPin");
            Prim(_pinRoot, PrimitiveType.Cylinder, PinColor,                                          new Vector3(0,.25f,0), new Vector3(.04f,.25f,.04f));
            Prim(_pinRoot, PrimitiveType.Sphere,   PinColor,                                          new Vector3(0,.55f,0), Vector3.one*.15f);
            Prim(_pinRoot, PrimitiveType.Cylinder, new Color(PinColor.r,PinColor.g,PinColor.b,.45f), new Vector3(0,.003f,0),new Vector3(.28f,.003f,.28f));
            Prim(_pinRoot, PrimitiveType.Cylinder, PinColor,                                          new Vector3(0,.82f,0), new Vector3(.03f,.1f,.03f));
            var h = Prim(_pinRoot, PrimitiveType.Cube, PinColor,                                     new Vector3(0,1f,0),   new Vector3(.12f,.12f,.12f));
            h.transform.localRotation = Quaternion.Euler(45f,45f,0f);
            _pinRing = Prim(_pinRoot, PrimitiveType.Cylinder,
                new Color(PinColor.r,PinColor.g,PinColor.b,.7f),
                new Vector3(0,.005f,0), new Vector3(.15f,.003f,.15f)); */
            PinObject.transform.position = _destWorld + Vector3.up * PinHeight;
        }

        void DestroyPin() { if (_pinRoot != null) { Destroy(_pinRoot); _pinRoot = _pinRing = null; } }

        void AnimatePin()
        {
         //   if (_pinRoot == null) return;
            _bobTimer += Time.deltaTime * 1.4f;
            PinObject.transform.position = _destWorld + Vector3.up * (PinHeight + Mathf.Sin(_bobTimer) * .05f);
            if (_pinRing != null)
            {
                _ringTimer += Time.deltaTime;
                float p = (_ringTimer % 1.3f) / 1.3f;
                float s = Mathf.Lerp(.12f, .65f, p);
                _pinRing.transform.localScale = new Vector3(s, .003f, s);
              //  var r = _pinRing.GetComponent<Renderer>();
              //  if (r != null) { var c = r.material.color; c.a = Mathf.Lerp(.8f,0f,p) * _alpha; r.material.color = c; }
            }
        }

        // ─── line material ────────────────────────────────────
        void BuildMaterial()
        {
            if (PathMaterial != null) { _mat = new Material(PathMaterial); return; }
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            _mat = new Material(sh);
            _mat.color = Color.magenta;
            Debug.LogWarning("[PathLine] No PathMaterial — magenta fallback.");
        }

        void SetupLR()
        {
            _lr.useWorldSpace     = true;
            _lr.positionCount     = PointCount;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows    = false;
            _lr.textureMode       = LineTextureMode.Tile;
            _lr.material          = _mat;
            _lr.enabled           = false;
            var wc = new AnimationCurve();
            wc.AddKey(0f, StartWidth); wc.AddKey(1f, EndWidth);
            _lr.widthCurve        = wc;
            _mat.mainTextureScale = new Vector2(10f, 1f);
        }

        void AnimateLine()
        {
            _scroll = (_scroll + Time.deltaTime * ScrollSpeed) % 1f;
            _mat.mainTextureOffset = new Vector2(-_scroll, 0f);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(LineColor, 0f), new GradientColorKey(LineColor, 1f) },
                new[] { new GradientAlphaKey(_alpha, 0f),    new GradientAlphaKey(_alpha * .08f, 1f) });
            _lr.colorGradient = g;
        }

        // ─── utilities ────────────────────────────────────────
        float GroundY()
        {
            if (Physics.Raycast(ARCamera.transform.position, Vector3.down, out RaycastHit hit, 10f))
                return hit.point.y;
            return ARCamera.transform.position.y - 1.6f;
        }

        float FlatDist(Vector3 a, Vector3 b) =>
            Vector3.Distance(new Vector3(a.x,0,a.z), new Vector3(b.x,0,b.z));

        GameObject Prim(GameObject parent, PrimitiveType type, Color col, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            var c = go.GetComponent<Collider>(); if (c) Destroy(c);
            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode",    3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color       = col;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }
    }
}
