using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARNavigation.Core;
using ARNavigation.Data;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// SIMPLE AR NAVIGATION
    /// ====================
    /// 1. Scan image  →  pin drops exactly DistanceMetres ahead of camera
    /// 2. Line draws from player feet to pin, updates every frame
    /// 3. Player walks to pin  →  "You Have Arrived!" UI appears
    /// 4. Player taps Arrived  →  clears and moves to next stop
    ///
    /// SETUP:
    ///   1. Create Empty GO "PathLine" at (0,0,0)  →  Add this script
    ///   2. Drag AR Camera into AR Camera slot
    ///   3. Create Material: Shader=Unlit/Color  Color=R0 G220 B200 A255  →  Path Material slot
    ///   4. Tick ForceShowLine to test. Adjust GroundOffset until line is on floor. Untick.
    ///   5. Wire up ArrivedPanel and ArrivedButton in Inspector
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ARPathLineRenderer : MonoBehaviour
    {
        // ─── REQUIRED ────────────────────────────────────────────────────────────
        [Header("REQUIRED")]
        [Tooltip("Drag AR Camera (child of ARSessionOrigin) here.")]
        public Camera ARCamera;

        [Tooltip("Unlit/Color material. Color R:0 G:220 B:200 A:255\nIf empty → magenta fallback.")]
        public Material PathMaterial;

        // ─── ARRIVED UI ──────────────────────────────────────────────────────────
        [Header("Arrived UI")]
        [Tooltip("The panel that shows when player reaches destination. Initially disabled.")]
        public GameObject ArrivedPanel;

        [Tooltip("The button inside ArrivedPanel. Wire its OnClick to OnArrivedPressed().")]
        public Button ArrivedButton;

        [Tooltip("Text inside arrived panel.")]
        public TextMeshProUGUI ArrivedText;

        [Tooltip("How close (metres) player must be to pin to show arrived UI.")]
        [Range(0.3f, 3f)]
        public float ArrivalRadius = 1.5f;

        // ─── TEST ────────────────────────────────────────────────────────────────
        [Header("Test / Debug")]
        [Tooltip("Shows pin and line immediately without scanning. Adjust GroundOffset with this on.")]
        public bool ForceShowLine = false;

        [Tooltip("Logs status every second to Console.")]
        public bool VerboseLogging = true;

        // ─── LINE ────────────────────────────────────────────────────────────────
        [Header("Line")]
        [Range(4, 40)]
        public int   PointCount   = 20;

        [Tooltip("Raise if line clips into floor. Start: 0.05")]
        [Range(0f, 0.5f)]
        public float GroundOffset = 0.05f;

        public float StartWidth   = 0.10f;
        public float EndWidth     = 0.03f;

        [Tooltip("Teal. R:0 G:220 B:200 A:230")]
        public Color LineColor    = new Color(0f, 0.86f, 0.78f, 0.9f);

        [Range(0f, 3f)]
        public float ScrollSpeed  = 0.7f;

        // ─── PIN ─────────────────────────────────────────────────────────────────
        [Header("Destination Pin")]
        public Color PinColor     = new Color(0f, 0.95f, 0.8f, 1f);

        [Tooltip("How high pin floats. 0 = on the floor.")]
        public float PinHeight    = 0f;

        // ─── PRIVATE ─────────────────────────────────────────────────────────────
        LineRenderer _lr;
        Material     _mat;

        Vector3    _destWorld;          // locked world position of destination
        bool       _destLocked;         // true after image scanned
        bool       _active;             // true while navigating
        bool       _arrivedShown;       // true once arrived UI is shown

        float      _alpha;
        float      _scroll;
        float      _bobTimer;
        float      _ringTimer;
        float      _logTimer;

        GameObject _pinRoot;
        GameObject _pinRing;

        // ─────────────────────────────────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────────
        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            BuildMaterial();
            SetupLineRenderer();

            // Make sure arrived panel starts hidden
            SetVisible(ArrivedPanel, false);
        }

        void OnEnable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged += OnNavState;

            if (ArrivedButton != null)
                ArrivedButton.onClick.AddListener(OnArrivedPressed);
        }

        void OnDisable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged -= OnNavState;

            if (ArrivedButton != null)
                ArrivedButton.onClick.RemoveListener(OnArrivedPressed);
        }

        void OnDestroy() { if (_mat) Destroy(_mat); }

        // ─────────────────────────────────────────────────────────────────────────
        //  UPDATE — runs every frame
        // ─────────────────────────────────────────────────────────────────────────
        void Update()
        {
            // Auto-find camera if not assigned
            if (ARCamera == null) { ARCamera = Camera.main; if (ARCamera == null) return; }

            // ForceShowLine test mode — drop pin 5m ahead immediately
            if (ForceShowLine && !_destLocked)
                LockDestination(5f);

            bool show = _active || ForceShowLine;

            // ── Fade in / out ─────────────────────────────────────────────────────
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime / 0.35f);

            if (_alpha < 0.01f)
            {
                _lr.enabled = false;
                SetVisible(_pinRoot, false);
                return;
            }

            _lr.enabled = true;
            SetVisible(_pinRoot, true);

            // ── Draw line + check arrival ─────────────────────────────────────────
            if (_destLocked)
            {
                DrawLine();
                CheckArrival();
            }

            // ── Animate texture scroll ────────────────────────────────────────────
            _scroll = (_scroll + Time.deltaTime * ScrollSpeed) % 1f;
            _mat.mainTextureOffset = new Vector2(-_scroll, 0f);
            ApplyAlpha(_alpha);

            // ── Animate pin ───────────────────────────────────────────────────────
            AnimatePin();

            // ── Verbose log ───────────────────────────────────────────────────────
            if (VerboseLogging)
            {
                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = 1f;
                    float dist = _destLocked
                        ? Vector3.Distance(
                            new Vector3(ARCamera.transform.position.x, 0, ARCamera.transform.position.z),
                            new Vector3(_destWorld.x, 0, _destWorld.z))
                        : -1f;
                    Debug.Log($"[PathLine] active={_active}  destLocked={_destLocked}  " +
                              $"distToDest={dist:F2}m  arrivalRadius={ArrivalRadius}m  " +
                              $"groundY={GroundY():F3}  alpha={_alpha:F2}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  LOCK DESTINATION  —  called once when image is scanned
        // ─────────────────────────────────────────────────────────────────────────
        void LockDestination(float metres)
        {
            // Flat forward direction of camera (ignore up/down tilt)
            Vector3 fwd = ARCamera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            float groundY = GroundY();

            // Pin world position = camera XZ + forward * metres, on the ground
            _destWorld = new Vector3(
                ARCamera.transform.position.x + fwd.x * metres,
                groundY,
                ARCamera.transform.position.z + fwd.z * metres
            );

            _destLocked   = true;
            _arrivedShown = false;
            _bobTimer     = 0f;
            _ringTimer    = 0f;

            BuildPin();

            SetVisible(ArrivedPanel, false);

            Debug.Log($"[PathLine] Pin locked {metres}m ahead at {_destWorld}  fwd={fwd}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  DRAW LINE — player feet → destination, every frame
        // ─────────────────────────────────────────────────────────────────────────
        void DrawLine()
        {
            float gy = GroundY() + GroundOffset;

            // Start = directly below camera on the ground
            Vector3 lineStart = new Vector3(
                ARCamera.transform.position.x, gy,
                ARCamera.transform.position.z);

            // End = destination pin position on the ground
            Vector3 lineEnd = new Vector3(_destWorld.x, gy, _destWorld.z);

            for (int i = 0; i < PointCount; i++)
            {
                float   t  = (float)i / (PointCount - 1);
                _lr.SetPosition(i, Vector3.Lerp(lineStart, lineEnd, t));
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  CHECK ARRIVAL — show arrived UI when close enough
        // ─────────────────────────────────────────────────────────────────────────
        void CheckArrival()
        {
            if (_arrivedShown) return;

            // Measure flat XZ distance only (ignore height difference)
            Vector3 playerFlat = new Vector3(ARCamera.transform.position.x, 0,
                                             ARCamera.transform.position.z);
            Vector3 destFlat   = new Vector3(_destWorld.x, 0, _destWorld.z);
            float   dist       = Vector3.Distance(playerFlat, destFlat);

            if (dist <= ArrivalRadius)
            {
                _arrivedShown = true;
                SetVisible(ArrivedPanel, true);

                if (ArrivedText != null)
                    ArrivedText.text = "You Have Arrived!\n" +
                                       (RouteManager.Instance?.CurrentStop?.DisplayName ?? "");

                Debug.Log($"[PathLine] ARRIVED at destination! Distance={dist:F2}m");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ARRIVED BUTTON
        // ─────────────────────────────────────────────────────────────────────────
        public void OnArrivedPressed()
        {
            SetVisible(ArrivedPanel, false);
            RouteManager.Instance?.ConfirmArrival();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  PIN — built from Unity primitives, no art needed
        // ─────────────────────────────────────────────────────────────────────────
        void BuildPin()
        {
            DestroyPin();
            _pinRoot = new GameObject("DestPin");

            // ── Pole ─────────────────────────────────────────────────────────────
            var pole = Prim(_pinRoot, PrimitiveType.Cylinder, PinColor);
            pole.transform.localPosition = new Vector3(0, 0.25f, 0);
            pole.transform.localScale    = new Vector3(0.04f, 0.25f, 0.04f);

            // ── Ball top ──────────────────────────────────────────────────────────
            var ball = Prim(_pinRoot, PrimitiveType.Sphere, PinColor);
            ball.transform.localPosition = new Vector3(0, 0.55f, 0);
            ball.transform.localScale    = Vector3.one * 0.15f;

            // ── Ground disc ───────────────────────────────────────────────────────
            var disc = Prim(_pinRoot, PrimitiveType.Cylinder,
                new Color(PinColor.r, PinColor.g, PinColor.b, 0.45f));
            disc.transform.localPosition = new Vector3(0, 0.003f, 0);
            disc.transform.localScale    = new Vector3(0.28f, 0.003f, 0.28f);

            // ── Arrow above ball (pointing up = destination marker) ───────────────
            var arrowShaft = Prim(_pinRoot, PrimitiveType.Cylinder, PinColor);
            arrowShaft.transform.localPosition = new Vector3(0, 0.82f, 0);
            arrowShaft.transform.localScale    = new Vector3(0.03f, 0.1f, 0.03f);

            var arrowHead = Prim(_pinRoot, PrimitiveType.Cube, PinColor);
            arrowHead.transform.localPosition = new Vector3(0, 1.0f, 0);
            arrowHead.transform.localScale    = new Vector3(0.12f, 0.12f, 0.12f);
            arrowHead.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

            // ── Pulse ring ────────────────────────────────────────────────────────
            _pinRing = Prim(_pinRoot, PrimitiveType.Cylinder,
                new Color(PinColor.r, PinColor.g, PinColor.b, 0.7f));
            _pinRing.transform.localPosition = new Vector3(0, 0.005f, 0);
            _pinRing.transform.localScale    = new Vector3(0.15f, 0.003f, 0.15f);

            _pinRoot.transform.position = _destWorld + Vector3.up * PinHeight;
        }

        void DestroyPin()
        {
            if (_pinRoot) { Destroy(_pinRoot); _pinRoot = null; _pinRing = null; }
        }

        void AnimatePin()
        {
            if (_pinRoot == null) return;

            // Bob pin up and down gently
            _bobTimer += Time.deltaTime * 1.4f;
            float bob = Mathf.Sin(_bobTimer) * 0.05f;
            _pinRoot.transform.position = _destWorld + Vector3.up * (PinHeight + bob);

            // Pulse ring expands outward and fades
            if (_pinRing != null)
            {
                _ringTimer += Time.deltaTime;
                float p = (_ringTimer % 1.3f) / 1.3f;
                float s = Mathf.Lerp(0.12f, 0.65f, p);
                _pinRing.transform.localScale = new Vector3(s, 0.003f, s);

                var rend = _pinRing.GetComponent<Renderer>();
                if (rend != null)
                {
                    var c = rend.material.color;
                    c.a   = Mathf.Lerp(0.8f, 0f, p) * _alpha;
                    rend.material.color = c;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────────
        float GroundY()
        {
            // Raycast down to find AR plane
            if (Physics.Raycast(ARCamera.transform.position, Vector3.down, out RaycastHit hit, 10f))
                return hit.point.y;
            // Fallback: average standing height
            return ARCamera.transform.position.y - 1.6f;
        }

        GameObject Prim(GameObject parent, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent.transform, false);
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);

            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode",   3);
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

        static void SetVisible(GameObject go, bool v) { if (go != null) go.SetActive(v); }

        void BuildMaterial()
        {
            if (PathMaterial != null)
            {
                _mat = new Material(PathMaterial);
            }
            else
            {
                var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                _mat   = new Material(sh);
                _mat.color = Color.magenta;
                Debug.LogWarning("[PathLine] No PathMaterial assigned — showing MAGENTA line. " +
                                 "Create Material → Unlit/Color → teal color → assign to Path Material.");
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
            _lr.widthCurve            = wc;
            _mat.mainTextureScale     = new Vector2(10f, 1f);
        }

        void ApplyAlpha(float a)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(LineColor, 0f), new GradientColorKey(LineColor, 1f) },
                new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a * 0.08f, 1f) }
            );
            _lr.colorGradient = g;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ROUTE MANAGER EVENTS
        // ─────────────────────────────────────────────────────────────────────────
        void OnNavState(RouteManager.NavState state, NavigationStop stop)
        {
            switch (state)
            {
                case RouteManager.NavState.Navigating:
                    _active = true;
                    LockDestination(stop?.DistanceMetres > 0 ? stop.DistanceMetres : 5f);
                    break;

                case RouteManager.NavState.WaitingForImage:
                case RouteManager.NavState.Arrived:
                case RouteManager.NavState.RouteComplete:
                case RouteManager.NavState.Idle:
                    _active     = false;
                    _destLocked = false;
                    DestroyPin();
                    SetVisible(ArrivedPanel, false);
                    break;
            }
        }

            // called by ARSessionStabilityManager after a full AR reset
    public void ClearDestination()
    {
        _destLocked = false;
        _active     = false;
        DestroyPin();
        SetVisible(ArrivedPanel, false);
        Debug.Log("[PathLine] Destination cleared — waiting for rescan");
    }

    }
}

