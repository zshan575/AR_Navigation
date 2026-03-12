using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;       // XROrigin lives here in Unity 2022+
using ARNavigation.Core;
using ARNavigation.Navigation;

namespace ARNavigation.Core
{
    /// <summary>
    /// AR SESSION STABILITY MANAGER  —  XROrigin Edition (Unity 2022+)
    /// =================================================================
    /// Fixes ALL of these:
    ///
    ///  ✓  App goes background / comes back   → objects jump / reset to 0,0,0
    ///  ✓  Screen locks then unlocks          → AR session restarts, objects gone
    ///  ✓  Phone jerked / moved fast          → tracking lost, objects snap wrong
    ///  ✓  Objects shake/jitter constantly    → per-frame position smoothing
    ///
    /// HOW IT WORKS:
    ///   • Hooks into ARSession.stateChanged  to detect tracking loss/recovery
    ///   • Hooks into OnApplicationPause      to detect background/screen lock
    ///   • On tracking lost  → saves every object's world position and FREEZES it
    ///   • On tracking back  → waits RecoveryDelay seconds, then RESTORES positions
    ///   • If tracking lost > AutoResetSeconds → full ARSession reset, user rescans
    ///   • Every frame → jitter smoothing filter on all stabilized objects
    ///
    /// SETUP:
    ///   1. Create Empty GO  "ARStabilityManager"
    ///   2. Add Component  → ARSessionStabilityManager
    ///   3. Fill Inspector:
    ///        AR Session       → drag ARSession GO
    ///        XR Origin        → drag XROrigin GO
    ///        AR Camera        → drag AR Camera (child of XROrigin)
    ///   4. Stabilized Objects list:
    ///        Add your PathLine GO and any other AR world objects
    ///   5. Leave all other values at defaults
    /// </summary>
    public class ARSessionStabilityManager : MonoBehaviour
    {
        // ─── REQUIRED REFERENCES ─────────────────────────────────────────────────
        [Header("Required References")]
        [Tooltip("Drag ARSession GameObject here.")]
        public ARSession ARSession;

        [Tooltip("Drag XROrigin GameObject here (Unity 2022+ replacement for ARSessionOrigin).")]
        public XROrigin XROrigin;

        [Tooltip("Drag the AR Camera (child of XROrigin > Camera Offset > Main Camera).")]
        public Camera ARCamera;

        // ─── OBJECTS TO STABILIZE ────────────────────────────────────────────────
        [Header("Objects To Stabilize")]
        [Tooltip("Add every AR world-space object: PathLine, Arrow, Pin, etc.\n" +
                 "These will be frozen when tracking is lost and restored after.")]
        public GameObject[] StabilizedObjects;

        // ─── RECOVERY SETTINGS ───────────────────────────────────────────────────
        [Header("Recovery Settings")]
        [Tooltip("Seconds to wait after tracking recovers before restoring objects.\n" +
                 "Increase if objects still jump on resume. Default: 0.6")]
        [Range(0.1f, 4f)]
        public float RecoveryDelay = 0.6f;

        [Tooltip("If tracking is lost longer than this, do a full AR session reset.\n" +
                 "Set 0 to never auto-reset. Default: 10")]
        [Range(0f, 30f)]
        public float AutoResetSeconds = 10f;

        // ─── JITTER SMOOTHING ─────────────────────────────────────────────────────
        [Header("Jitter Smoothing")]
        [Tooltip("Smooths small jitter on AR objects each frame.\n" +
                 "Higher = smoother but slight lag. 0 = off. Default: 15")]
        [Range(0f, 30f)]
        public float SmoothingSpeed = 15f;

        [Tooltip("Only smooth if object moved less than this per frame (metres).\n" +
                 "Spikes above this are tracking errors and get clamped. Default: 0.08")]
        [Range(0.01f, 0.5f)]
        public float JitterThreshold = 0.08f;

        // ─── DEBUG ───────────────────────────────────────────────────────────────
        [Header("Debug")]
        public bool VerboseLogging = true;

        // ─── PRIVATE STATE ────────────────────────────────────────────────────────
        bool        _trackingLost;
        float       _trackingLostTimer;
        bool        _appPaused;
        Coroutine   _recoveryRoutine;

        // Per-object saved state
        Vector3[]    _savedPos;
        Quaternion[] _savedRot;
        bool[]       _wasActive;

        // Per-object smooth positions (jitter filter)
        Vector3[]    _smoothPos;
        bool         _smoothReady;

        // ─── LIFECYCLE ────────────────────────────────────────────────────────────
        void Awake()
        {
            // Auto-find if not assigned
            if (ARSession  == null) ARSession  = FindObjectOfType<ARSession>();
            if (XROrigin   == null) XROrigin   = FindObjectOfType<XROrigin>();
            if (ARCamera   == null) ARCamera   = Camera.main;

            int n = StabilizedObjects?.Length ?? 0;
            _savedPos  = new Vector3[n];
            _savedRot  = new Quaternion[n];
            _wasActive = new bool[n];
            _smoothPos = new Vector3[n];
        }

        void OnEnable()
        {
            ARSession.stateChanged += OnARStateChanged;
        }

        void OnDisable()
        {
            ARSession.stateChanged -= OnARStateChanged;
        }

        void Start()
        {
            Log("ARSessionStabilityManager ready.");
            InitSmoothPositions();
        }

        // ─── EVERY FRAME ─────────────────────────────────────────────────────────
        void Update()
        {
            // Count up while tracking is lost
            if (_trackingLost)
            {
                _trackingLostTimer += Time.deltaTime;

                if (AutoResetSeconds > 0f && _trackingLostTimer >= AutoResetSeconds)
                {
                    Log($"Tracking lost {_trackingLostTimer:F1}s — auto resetting AR session");
                    _trackingLostTimer = 0f;
                    StartCoroutine(FullReset());
                }
            }

            // Jitter smoothing every frame (only when tracking is good)
            if (!_trackingLost && !_appPaused && SmoothingSpeed > 0f)
                DoJitterSmoothing();
        }

        // ─── APP BACKGROUND / SCREEN LOCK ────────────────────────────────────────
        void OnApplicationPause(bool paused)
        {
            _appPaused = paused;

            if (paused)
            {
                Log("App PAUSED (background / screen lock) — saving and freezing objects");
                SaveAll();
                HideAll();
            }
            else
            {
                Log("App RESUMED — starting recovery");
                if (_recoveryRoutine != null) StopCoroutine(_recoveryRoutine);
                _recoveryRoutine = StartCoroutine(RecoverFromPause());
            }
        }

        // ─── AR SESSION STATE CHANGE ──────────────────────────────────────────────
        void OnARStateChanged(ARSessionStateChangedEventArgs args)
        {
            Log($"ARSession state → {args.state}");

            switch (args.state)
            {
                // ── Tracking good ─────────────────────────────────────────────────
                case ARSessionState.SessionTracking:
                    if (_trackingLost)
                    {
                        Log("Tracking RECOVERED — scheduling restore");
                        _trackingLost      = false;
                        _trackingLostTimer = 0f;
                        if (_recoveryRoutine != null) StopCoroutine(_recoveryRoutine);
                        _recoveryRoutine = StartCoroutine(RestoreAfterDelay());
                    }
                    break;

                // ── Tracking degraded (jerk, fast movement, occlusion) ────────────
                case ARSessionState.SessionInitializing:
                    if (!_trackingLost)
                    {
                        Log("Tracking LOST — freezing objects in place");
                        SaveAll();
                        FreezeAll();    // keep visible but stop moving
                        _trackingLost      = true;
                        _trackingLostTimer = 0f;
                    }
                    break;

                case ARSessionState.None:
                case ARSessionState.Unsupported:
                case ARSessionState.CheckingAvailability:
                    break;
            }
        }

        // ─── COROUTINES ───────────────────────────────────────────────────────────

        IEnumerator RecoverFromPause()
        {
            Log("Waiting for AR session to re-initialize after resume...");

            // Wait for camera feed and ARCore to restart
            float waited = 0f;
            while (waited < 8f)
            {
                waited += Time.deltaTime;

                if (ARSession.state == ARSessionState.SessionTracking)
                {
                    Log($"Tracking restored after {waited:F1}s — restoring objects");
                    yield return new WaitForSeconds(RecoveryDelay);
                    ShowAll();
                    RestoreAll();
                    _appPaused = false;
                    yield break;
                }
                yield return null;
            }

            // Timed out → full reset
            Log("Recovery timed out after resume — doing full reset");
            yield return FullReset();
        }

        IEnumerator RestoreAfterDelay()
        {
            Log($"Waiting {RecoveryDelay}s before restoring objects...");
            yield return new WaitForSeconds(RecoveryDelay);
            ShowAll();
            RestoreAll();
            _smoothReady = false; // re-init smooth positions after restore
            Log("Objects restored after tracking recovery");
        }

        IEnumerator FullReset()
        {
            Log("=== FULL AR SESSION RESET ===");

            // 1. Hide everything
            HideAll();
            _trackingLost      = false;
            _trackingLostTimer = 0f;

            // 2. Restart ARSession
            if (ARSession != null)
            {
                ARSession.enabled = false;
                yield return new WaitForSeconds(0.3f);
                ARSession.Reset();
                ARSession.enabled = true;
            }

            // 3. Wait for tracking
            Log("Waiting for tracking after reset...");
            float waited = 0f;
            while (ARSession.state != ARSessionState.SessionTracking && waited < 12f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(RecoveryDelay);

            // 4. Tell PathLine to clear its locked destination (position is now invalid)
            ClearAllPathLines();

            // 5. Tell RouteManager to go back to scan screen
            if (RouteManager.Instance != null &&
                RouteManager.Instance.CurrentState == RouteManager.NavState.Navigating)
            {
                RouteManager.Instance.RequireRescan();
                Log("RouteManager told to rescan");
            }

            ShowAll();
            _smoothReady = false;
            Log("=== FULL RESET COMPLETE — user must rescan ===");
        }

        // ─── SAVE / RESTORE / SHOW / HIDE ────────────────────────────────────────

        void SaveAll()
        {
            if (StabilizedObjects == null) return;
            for (int i = 0; i < StabilizedObjects.Length; i++)
            {
                var go = StabilizedObjects[i];
                if (go == null) continue;
                _savedPos[i]  = go.transform.position;
                _savedRot[i]  = go.transform.rotation;
                _wasActive[i] = go.activeSelf;
            }
            Log($"Saved {StabilizedObjects.Length} object transforms");
        }

        void RestoreAll()
        {
            if (StabilizedObjects == null) return;
            for (int i = 0; i < StabilizedObjects.Length; i++)
            {
                var go = StabilizedObjects[i];
                if (go == null) continue;
                go.transform.position = _savedPos[i];
                go.transform.rotation = _savedRot[i];
            }
            Log("Restored all object transforms");
        }

        /// <summary>Keep objects visible but disable scripts that move them.</summary>
        void FreezeAll()
        {
            if (StabilizedObjects == null) return;
            foreach (var go in StabilizedObjects)
            {
                if (go == null) continue;
                // Disable ARPathLineRenderer so it stops moving the line
                var lr = go.GetComponent<ARPathLineRenderer>();
                if (lr != null) lr.enabled = false;
            }
        }

        void HideAll()
        {
            if (StabilizedObjects == null) return;
            foreach (var go in StabilizedObjects)
                if (go != null) go.SetActive(false);
        }

        void ShowAll()
        {
            if (StabilizedObjects == null) return;
            for (int i = 0; i < StabilizedObjects.Length; i++)
            {
                var go = StabilizedObjects[i];
                if (go == null) continue;
                go.SetActive(_wasActive[i]);

                // Re-enable scripts
                var lr = go.GetComponent<ARPathLineRenderer>();
                if (lr != null) lr.enabled = true;
            }
        }

        void ClearAllPathLines()
        {
            if (StabilizedObjects == null) return;
            foreach (var go in StabilizedObjects)
            {
                if (go == null) continue;
                var lr = go.GetComponent<ARPathLineRenderer>();
                if (lr != null) lr.ClearDestination();
            }
        }

        // ─── JITTER SMOOTHING ─────────────────────────────────────────────────────
        void InitSmoothPositions()
        {
            if (StabilizedObjects == null) return;
            for (int i = 0; i < StabilizedObjects.Length; i++)
                if (StabilizedObjects[i] != null)
                    _smoothPos[i] = StabilizedObjects[i].transform.position;
            _smoothReady = true;
        }

        void DoJitterSmoothing()
        {
            if (!_smoothReady || StabilizedObjects == null) return;

            for (int i = 0; i < StabilizedObjects.Length; i++)
            {
                var go = StabilizedObjects[i];
                if (go == null || !go.activeInHierarchy) continue;

                float delta = Vector3.Distance(go.transform.position, _smoothPos[i]);

                if (delta < JitterThreshold && delta > 0.001f)
                {
                    // Small jitter — smooth it
                    _smoothPos[i]         = Vector3.Lerp(_smoothPos[i], go.transform.position,
                                                          Time.deltaTime * SmoothingSpeed);
                    go.transform.position = _smoothPos[i];
                }
                else if (delta >= JitterThreshold)
                {
                    // Large intentional movement — follow without smoothing
                    _smoothPos[i] = go.transform.position;
                }
            }
        }

        void Log(string msg) { if (VerboseLogging) Debug.Log($"[ARStability] {msg}"); }
    }
}
