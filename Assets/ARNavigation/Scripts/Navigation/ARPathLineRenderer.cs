using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavigation.Core;
using ARNavigation.Data;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// AR PATH LINE RENDERER
    /// ======================
    /// Full navigation flow handled in ONE script:
    ///
    ///   Scan image  →  pin locks X metres ahead  →  line draws from feet to pin
    ///   Walk to pin →  ArrivedPanel shows ("You Have Arrived!")
    ///   Tap "Next Location"  →  pin+line clear  →  ScanPanel shows
    ///   Scan next image  →  repeat
    ///
    /// SETUP:
    ///   1. Create Empty GO "PathLine" at (0,0,0) → Add this script
    ///   2. Drag AR Camera into AR Camera slot
    ///   3. Create Material: Shader=Unlit/Color  Color=R0 G220 B200 A255 → Path Material
    ///   4. Wire the UI panels (HomePanel, ScanPanel, NavigatePanel, ArrivedPanel, CompletePanel)
    ///   5. Tick ForceShowLine to test. Adjust GroundOffset until line sits on floor. Untick.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ARPathLineRenderer : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════
        //  REQUIRED
        // ═══════════════════════════════════════════════════════
        [Header("── REQUIRED ───────────────────────────")]
        [Tooltip("Drag AR Camera (child of XROrigin) here.")]
        public Camera ARCamera;

        [Tooltip("Unlit/Color material. Color R:0 G:220 B:200 A:255\nLeave empty → magenta fallback.")]
        public Material PathMaterial;

        // ═══════════════════════════════════════════════════════
        //  UI PANELS  ← wire all of these in Inspector
        // ═══════════════════════════════════════════════════════
        [Header("── UI PANELS (wire all in Inspector) ──")]
        [Tooltip("First screen. Shown on launch.")]
        public GameObject HomePanel;

        [Tooltip("Camera scan screen. Shown while waiting for image scan.")]
        public GameObject ScanPanel;

        [Tooltip("Shown while navigating (line + pin active).")]
        public GameObject NavigatePanel;

        [Tooltip("Overlaid on NavigatePanel when player reaches destination.")]
        public GameObject ArrivedPanel;

        [Tooltip("Shown after all stops completed.")]
        public GameObject CompletePanel;

        // ═══════════════════════════════════════════════════════
        //  HOME PANEL
        // ═══════════════════════════════════════════════════════
        [Header("── HOME PANEL ─────────────────────────")]
        [Tooltip("'Start' button on home screen.")]
        public Button StartButton;

        [Tooltip("App title text on home screen.")]
        public TextMeshProUGUI AppTitleText;

        [Tooltip("Route description text. e.g. 'Office Tour — 3 stops'")]
        public TextMeshProUGUI RouteDescriptionText;

        // ═══════════════════════════════════════════════════════
        //  SCAN PANEL
        // ═══════════════════════════════════════════════════════
        [Header("── SCAN PANEL ─────────────────────────")]
        [Tooltip("e.g. 'STEP 1 OF 3'")]
        public TextMeshProUGUI ScanStepText;

        [Tooltip("e.g. 'Point camera at the marker'")]
        public TextMeshProUGUI ScanInstructionText;

        [Tooltip("e.g. 'Looking for: Reception'")]
        public TextMeshProUGUI ScanTargetText;

        // ═══════════════════════════════════════════════════════
        //  NAVIGATE PANEL
        // ═══════════════════════════════════════════════════════
        [Header("── NAVIGATE PANEL ─────────────────────")]
        [Tooltip("Destination name. e.g. 'Conference Room A'")]
        public TextMeshProUGUI NavDestinationText;

        [Tooltip("Step counter. e.g. 'Stop 2 of 3'")]
        public TextMeshProUGUI NavStepText;

        [Tooltip("Distance remaining. Updated live.")]
        public TextMeshProUGUI NavDistanceText;

        [Tooltip("Walking instruction. e.g. 'Turn left at the stairs'")]
        public TextMeshProUGUI NavInstructionText;

        // ═══════════════════════════════════════════════════════
        //  ARRIVED PANEL
        // ═══════════════════════════════════════════════════════
        [Header("── ARRIVED PANEL ──────────────────────")]
        [Tooltip("'You Have Arrived!' title text.")]
        public TextMeshProUGUI ArrivedTitleText;

        [Tooltip("Stop name shown on arrived screen.")]
        public TextMeshProUGUI ArrivedStopNameText;

        [Tooltip("'Next Location' button. Clears line+pin and opens scan screen.")]
        public Button NextLocationButton;

        [Tooltip("Text on the next button. Auto-changes to 'Finish' on last stop.")]
        public TextMeshProUGUI NextLocationButtonText;

        // ═══════════════════════════════════════════════════════
        //  COMPLETE PANEL
        // ═══════════════════════════════════════════════════════
        [Header("── COMPLETE PANEL ─────────────────────")]
        public TextMeshProUGUI CompleteTitleText;
        public TextMeshProUGUI CompleteSubText;
        public Button          RestartButton;

        // ═══════════════════════════════════════════════════════
        //  LINE SETTINGS
        // ═══════════════════════════════════════════════════════
        [Header("── LINE ───────────────────────────────")]
        [Range(4, 40)]
        public int   PointCount   = 20;

        [Tooltip("Raise if line clips into floor. Start: 0.05")]
        [Range(0f, 0.5f)]
        public float GroundOffset = 0.05f;

        public float StartWidth   = 0.10f;
        public float EndWidth     = 0.03f;

        [Tooltip("Teal: R:0 G:220 B:200 A:230")]
        public Color LineColor    = new Color(0f, 0.86f, 0.78f, 0.9f);

        [Range(0f, 3f)]
        public float ScrollSpeed  = 0.7f;

        [Header("── LINE ANIMATIONS ────────────────────")]
        public bool  GlowBreathEnabled = true;
        [Range(0f,1f)] public float GlowBreathMin   = 0.35f;
        [Range(0f,1f)] public float GlowBreathMax   = 1.0f;
        public float GlowBreathSpeed   = 1.6f;
        public bool  WidthPulseEnabled = true;
        [Range(0f,0.05f)] public float WidthPulseAmount = 0.025f;
        public bool  ColorShiftEnabled = true;
        public Color LineColorHighlight = new Color(0.2f, 0.6f, 1f, 0.9f);

        // ═══════════════════════════════════════════════════════
        //  PIN SETTINGS
        // ═══════════════════════════════════════════════════════
        [Header("── DESTINATION PIN ──────────────────────")]
        public Color PinColor  = new Color(0f, 0.95f, 0.8f, 1f);

        [Tooltip("How high pin floats above ground. 0 = floor level.")]
        public float PinHeight = 0f;

        // ═══════════════════════════════════════════════════════
        //  ARRIVAL
        // ═══════════════════════════════════════════════════════
        [Header("── ARRIVAL ─────────────────────────────")]
        [Tooltip("How close (metres) player must be to trigger Arrived. Default: 1.5")]
        [Range(0.3f, 5f)]
        public float ArrivalRadius = 1.5f;

        // ═══════════════════════════════════════════════════════
        //  TEST / DEBUG
        // ═══════════════════════════════════════════════════════
        [Header("── TEST / DEBUG ────────────────────────")]
        [Tooltip("Show line immediately without scanning. Use to set up GroundOffset.")]
        public bool ForceShowLine = false;

        [Tooltip("Log status to Console every second.")]
        public bool VerboseLogging = true;

        // ═══════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ═══════════════════════════════════════════════════════
        LineRenderer _lr;
        Material     _mat;

        Vector3 _destWorld;
        bool    _destLocked;
        bool    _active;
        bool    _arrivedShown;

        // Public access for ARSessionStabilityManager
        public Vector3 DestinationWorld  => _destWorld;
        public bool    DestinationLocked => _destLocked;

        float _alpha;
        float _scroll;
        float _bobTimer;
        float _ringTimer;
        float _logTimer;
        float _glowTimer;
        float _baseStartWidth;
        float _baseEndWidth;

        GameObject _pinRoot;
        GameObject _pinRing;

        RouteManager _rm;

        // ═══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════
        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            BuildMaterial();
            SetupLineRenderer();
        }

        void Start()
        {
            _rm = RouteManager.Instance;

            // Wire buttons
            if (StartButton        != null) StartButton.onClick.AddListener(OnStartPressed);
            if (NextLocationButton != null) NextLocationButton.onClick.AddListener(OnNextLocationPressed);
            if (RestartButton      != null) RestartButton.onClick.AddListener(OnRestartPressed);

            // Subscribe to route state
            if (_rm != null) _rm.OnStateChanged += OnNavState;

            // Start on home screen
            ShowHome();
        }

        void OnDestroy()
        {
            if (_rm != null) _rm.OnStateChanged -= OnNavState;
            if (_mat) Destroy(_mat);
        }

        // ═══════════════════════════════════════════════════════
        //  UPDATE
        // ═══════════════════════════════════════════════════════
        void Update()
        {
            if (ARCamera == null) { ARCamera = Camera.main; if (ARCamera == null) return; }

            // Test mode: force pin 5m ahead
            if (ForceShowLine && !_destLocked)
                LockDestination(5f);

            bool show = _active || ForceShowLine;
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime / 0.35f);

            if (_alpha < 0.01f)
            {
                _lr.enabled = false;
                Show(_pinRoot, false);
                return;
            }

            _lr.enabled = true;
            Show(_pinRoot, true);

            if (_destLocked)
            {
                DrawLine();
                CheckArrival();
                UpdateDistanceText();
            }

            // Line animations
            _scroll = (_scroll + Time.deltaTime * ScrollSpeed) % 1f;
            _mat.mainTextureOffset = new Vector2(-_scroll, 0f);
            ApplyAlpha(_alpha);
            AnimateLineEffects();
            AnimatePin();

            // Verbose log
            if (VerboseLogging)
            {
                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = 1f;
                    float d = _destLocked ? FlatDist(ARCamera.transform.position, _destWorld) : -1f;
                    Debug.Log($"[PathLine] active={_active} locked={_destLocked} dist={d:F1}m alpha={_alpha:F2}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  SCREEN FLOW
        // ═══════════════════════════════════════════════════════
        void ShowHome()
        {
            ShowOnly(HomePanel);
            _active     = false;
            _destLocked = false;
            DestroyPin();

            if (AppTitleText != null)
                AppTitleText.text = "AR Navigator";

            if (RouteDescriptionText != null && _rm != null)
                RouteDescriptionText.text = $"{_rm.Route.RouteName}  •  {_rm.TotalSteps} stops";
        }

        void ShowScan()
        {
            ShowOnly(ScanPanel);
            // Hide line + pin
            _active     = false;
            _destLocked = false;
            DestroyPin();

            if (_rm == null) return;
            int step  = _rm.CurrentStepIndex + 1;
            int total = _rm.TotalSteps;
            var stop  = _rm.CurrentStop;

            if (ScanStepText != null)
                ScanStepText.text = $"STEP {step} OF {total}";

            if (ScanInstructionText != null)
                ScanInstructionText.text = "Point the camera at\nthe location marker";

            if (ScanTargetText != null && stop != null)
                ScanTargetText.text = $"Looking for:\n<b>{stop.DisplayName}</b>";
        }

        void ShowNavigate(NavigationStop stop)
        {
            ShowOnly(NavigatePanel);
            Show(ArrivedPanel, false);   // make sure arrived is hidden
            _arrivedShown = false;

            if (stop == null) return;
            if (NavDestinationText != null)  NavDestinationText.text = stop.DisplayName;
            if (NavInstructionText != null)  NavInstructionText.text = stop.Instruction;
            if (NavStepText != null && _rm != null)
                NavStepText.text = $"Stop {_rm.CurrentStepIndex + 1} of {_rm.TotalSteps}";
        }

        void ShowArrived(NavigationStop stop)
        {
            // Keep NavigatePanel visible underneath — just show ArrivedPanel on top
            Show(NavigatePanel, true);
            Show(ArrivedPanel,  true);

            if (ArrivedTitleText != null)
                ArrivedTitleText.text = "You Have Arrived!";

            if (ArrivedStopNameText != null)
                ArrivedStopNameText.text = stop?.DisplayName ?? "";

            bool isLast = _rm != null && _rm.CurrentStepIndex >= _rm.TotalSteps - 1;
            if (NextLocationButtonText != null)
                NextLocationButtonText.text = isLast ? "Finish Route" : "Next Location";
        }

        void ShowComplete()
        {
            ShowOnly(CompletePanel);
            _active     = false;
            _destLocked = false;
            DestroyPin();

            if (CompleteTitleText != null)
                CompleteTitleText.text = "Route Complete!";

            if (CompleteSubText != null && _rm != null)
                CompleteSubText.text = $"You visited all {_rm.TotalSteps} locations.\nWell done!";
        }

        // ═══════════════════════════════════════════════════════
        //  BUTTON HANDLERS
        // ═══════════════════════════════════════════════════════
        void OnStartPressed()
        {
            Debug.Log("[PathLine] Start pressed");
            _rm?.StartNavigation();
            // ShowScan() triggered automatically by OnNavState → WaitingForImage
        }

        void OnNextLocationPressed()
        {
            Debug.Log("[PathLine] Next Location pressed");
            // Hide arrived panel immediately
            Show(ArrivedPanel, false);
            // Tell RouteManager to advance to next stop
            _rm?.ConfirmArrival();
            // OnNavState fires → WaitingForImage → ShowScan()  OR  RouteComplete → ShowComplete()
        }

        void OnRestartPressed()
        {
            Debug.Log("[PathLine] Restart pressed");
            _rm?.ResetRoute();
            ShowHome();
        }

        // ═══════════════════════════════════════════════════════
        //  ROUTE MANAGER STATE HANDLER
        // ═══════════════════════════════════════════════════════
        void OnNavState(RouteManager.NavState state, NavigationStop stop)
        {
            Debug.Log($"[PathLine] NavState → {state}");
            switch (state)
            {
                case RouteManager.NavState.WaitingForImage:
                    ShowScan();
                    break;

                case RouteManager.NavState.Navigating:
                    _active = true;
                    LockDestination(stop?.DistanceMetres > 0 ? stop.DistanceMetres : 5f);
                    ShowNavigate(stop);
                    break;

                case RouteManager.NavState.RouteComplete:
                    ShowComplete();
                    break;

                case RouteManager.NavState.Idle:
                    ShowHome();
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  DESTINATION LOCK
        // ═══════════════════════════════════════════════════════
        void LockDestination(float metres)
        {
            Vector3 fwd = ARCamera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            float gy   = GroundY();
            _destWorld = new Vector3(
                ARCamera.transform.position.x + fwd.x * metres,
                gy,
                ARCamera.transform.position.z + fwd.z * metres);

            _destLocked   = true;
            _arrivedShown = false;
            _bobTimer     = 0f;
            _ringTimer    = 0f;

            BuildPin();
            Debug.Log($"[PathLine] Pin locked {metres}m ahead at {_destWorld}");
        }

        // ═══════════════════════════════════════════════════════
        //  DRAW LINE
        // ═══════════════════════════════════════════════════════
        void DrawLine()
        {
            float gy = GroundY() + GroundOffset;
            Vector3 start = new Vector3(ARCamera.transform.position.x, gy, ARCamera.transform.position.z);
            Vector3 end   = new Vector3(_destWorld.x, gy, _destWorld.z);

            for (int i = 0; i < PointCount; i++)
                _lr.SetPosition(i, Vector3.Lerp(start, end, (float)i / (PointCount - 1)));
        }

        // ═══════════════════════════════════════════════════════
        //  ARRIVAL CHECK
        // ═══════════════════════════════════════════════════════
        void CheckArrival()
        {
            if (_arrivedShown) return;
            if (FlatDist(ARCamera.transform.position, _destWorld) <= ArrivalRadius)
            {
                _arrivedShown = true;
                ShowArrived(_rm?.CurrentStop);
                Debug.Log("[PathLine] ARRIVED!");
            }
        }

        void UpdateDistanceText()
        {
            if (NavDistanceText == null) return;
            float d = FlatDist(ARCamera.transform.position, _destWorld);
            NavDistanceText.text = d > 1f ? $"{d:F1} m away" : "Almost there!";
        }

        // ═══════════════════════════════════════════════════════
        //  PIN
        // ═══════════════════════════════════════════════════════
        void BuildPin()
        {
            DestroyPin();
            _pinRoot = new GameObject("DestPin");

            var pole = Prim(_pinRoot, PrimitiveType.Cylinder, PinColor);
            pole.transform.localPosition = new Vector3(0, 0.25f, 0);
            pole.transform.localScale    = new Vector3(0.04f, 0.25f, 0.04f);

            var ball = Prim(_pinRoot, PrimitiveType.Sphere, PinColor);
            ball.transform.localPosition = new Vector3(0, 0.55f, 0);
            ball.transform.localScale    = Vector3.one * 0.15f;

            var disc = Prim(_pinRoot, PrimitiveType.Cylinder, new Color(PinColor.r, PinColor.g, PinColor.b, 0.45f));
            disc.transform.localPosition = new Vector3(0, 0.003f, 0);
            disc.transform.localScale    = new Vector3(0.28f, 0.003f, 0.28f);

            var shaft = Prim(_pinRoot, PrimitiveType.Cylinder, PinColor);
            shaft.transform.localPosition = new Vector3(0, 0.82f, 0);
            shaft.transform.localScale    = new Vector3(0.03f, 0.1f, 0.03f);

            var head = Prim(_pinRoot, PrimitiveType.Cube, PinColor);
            head.transform.localPosition = new Vector3(0, 1.0f, 0);
            head.transform.localScale    = new Vector3(0.12f, 0.12f, 0.12f);
            head.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

            _pinRing = Prim(_pinRoot, PrimitiveType.Cylinder, new Color(PinColor.r, PinColor.g, PinColor.b, 0.7f));
            _pinRing.transform.localPosition = new Vector3(0, 0.005f, 0);
            _pinRing.transform.localScale    = new Vector3(0.15f, 0.003f, 0.15f);

            _pinRoot.transform.position = _destWorld + Vector3.up * PinHeight;
        }

        void DestroyPin() { if (_pinRoot) { Destroy(_pinRoot); _pinRoot = null; _pinRing = null; } }

        void AnimatePin()
        {
            if (_pinRoot == null) return;
            _bobTimer += Time.deltaTime * 1.4f;
            _pinRoot.transform.position = _destWorld + Vector3.up * (PinHeight + Mathf.Sin(_bobTimer) * 0.05f);

            if (_pinRing != null)
            {
                _ringTimer += Time.deltaTime;
                float p = (_ringTimer % 1.3f) / 1.3f;
                _pinRing.transform.localScale = new Vector3(Mathf.Lerp(0.12f, 0.65f, p), 0.003f, Mathf.Lerp(0.12f, 0.65f, p));
                var rend = _pinRing.GetComponent<Renderer>();
                if (rend != null) { var c = rend.material.color; c.a = Mathf.Lerp(0.8f, 0f, p) * _alpha; rend.material.color = c; }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  CALLED BY ARSessionStabilityManager
        // ═══════════════════════════════════════════════════════
        public void ClearDestination()
        {
            _destLocked = false;
            _active     = false;
            DestroyPin();
            Debug.Log("[PathLine] Destination cleared — waiting for rescan");
        }

        // ═══════════════════════════════════════════════════════
        //  LINE MATERIAL & ANIMATIONS
        // ═══════════════════════════════════════════════════════
        void BuildMaterial()
        {
            if (PathMaterial != null) { _mat = new Material(PathMaterial); }
            else
            {
                var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _mat = new Material(sh);
                _mat.color = Color.magenta;
                Debug.LogWarning("[PathLine] No PathMaterial — magenta fallback. Create Unlit/Color material.");
            }
        }

        void SetupLineRenderer()
        {
            _lr.useWorldSpace     = true;
            _lr.positionCount     = PointCount;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows    = false;
            _lr.textureMode       = LineTextureMode.Tile;
            _lr.material          = _mat;
            _lr.enabled           = false;
            var wc = new AnimationCurve();
            wc.AddKey(0f, StartWidth);
            wc.AddKey(1f, EndWidth);
            _lr.widthCurve        = wc;
            _mat.mainTextureScale = new Vector2(10f, 1f);
            _baseStartWidth       = StartWidth;
            _baseEndWidth         = EndWidth;
        }

        void ApplyAlpha(float a)
        {
            float t       = (_glowTimer > 0f) ? (Mathf.Sin(_glowTimer * GlowBreathSpeed) + 1f) * 0.5f : 1f;
            float breathA = GlowBreathEnabled ? Mathf.Lerp(GlowBreathMin, GlowBreathMax, t) : 1f;
            Color mid     = Color.Lerp(LineColor, ColorShiftEnabled ? LineColorHighlight : LineColor, t * 0.5f);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(mid, 0f),         new GradientColorKey(LineColor, 1f) },
                new[] { new GradientAlphaKey(a * breathA, 0f), new GradientAlphaKey(a * breathA * 0.08f, 1f) });
            _lr.colorGradient = g;
        }

        void AnimateLineEffects()
        {
            _glowTimer += Time.deltaTime;
            if (!WidthPulseEnabled) return;
            float t   = (Mathf.Sin(_glowTimer * GlowBreathSpeed) + 1f) * 0.5f;
            float p   = Mathf.Lerp(-WidthPulseAmount, WidthPulseAmount, t);
            var wc    = new AnimationCurve();
            wc.AddKey(0f, _baseStartWidth + p);
            wc.AddKey(1f, _baseEndWidth   + p * 0.5f);
            _lr.widthCurve = wc;
        }

        // ═══════════════════════════════════════════════════════
        //  UTILITY
        // ═══════════════════════════════════════════════════════
        float GroundY()
        {
            if (Physics.Raycast(ARCamera.transform.position, Vector3.down, out RaycastHit hit, 10f))
                return hit.point.y;
            return ARCamera.transform.position.y - 1.6f;
        }

        float FlatDist(Vector3 a, Vector3 b) =>
            Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));

        void ShowOnly(GameObject target)
        {
            Show(HomePanel,     target == HomePanel);
            Show(ScanPanel,     target == ScanPanel);
            Show(NavigatePanel, target == NavigatePanel);
            Show(ArrivedPanel,  false);
            Show(CompletePanel, target == CompletePanel);
        }

        static void Show(GameObject go, bool v) { if (go != null) go.SetActive(v); }

        GameObject Prim(GameObject parent, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent.transform, false);
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode",    3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color       = color;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }
    }
}
