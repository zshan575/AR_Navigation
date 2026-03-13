using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARNavigation.Core;

namespace ARNavigation.ImageTracking
{
    /// <summary>
    /// Replaces QRScanner entirely.
    /// Uses AR Foundation's ARTrackedImageManager to recognise physical images
    /// (posters, signs, printed cards) that were added to the
    /// XR Reference Image Library in the Unity Editor.
    ///
    /// HOW IT WORKS:
    ///   1. You import a photo of the physical marker into Unity.
    ///   2. You add it to an XR Reference Image Library (name it e.g. "stop_reception").
    ///   3. ARCore learns the image's visual features at build-time.
    ///   4. At runtime, when the device camera sees that image, ARCore fires an
    ///      event here, and we forward the image name to RouteManager.
    ///
    /// SETUP:
    ///   • Attach to the ARSessionOrigin GameObject.
    ///   • An ARTrackedImageManager component must also be on ARSessionOrigin
    ///     (Unity adds it for you — see setup guide).
    ///   • Assign your XR Reference Image Library to ARTrackedImageManager.
    ///   • Optionally assign MarkerOverlayPrefab to show a visual ping on the image.
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ARImageTracker : MonoBehaviour
    {
        public static ARImageTracker Instance { get; private set; }
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Marker Overlay (optional)")]
        [Tooltip("If assigned, this prefab is instantiated on top of the recognised " +
                 "image in world-space (e.g. a glowing frame or tick icon).")]
        public GameObject MarkerOverlayPrefab;

        [Tooltip("How long the overlay stays visible before auto-destroying (0 = forever).")]
        public float OverlayLifetimeSeconds = 2.5f;

        [Header("Debug")]
        public bool VerboseLogging = true;

        // ── Private ───────────────────────────────────────────────────────────────
        ARTrackedImageManager _imageManager;

        // Tracks overlays we have spawned so we don't spam-spawn on every frame
        readonly Dictionary<TrackableId, GameObject> _overlays
            = new Dictionary<TrackableId, GameObject>();

        // Prevent the same image firing twice in quick succession
        readonly HashSet<string> _recognisedThisRound = new HashSet<string>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        void Awake()
        {
            _imageManager = GetComponent<ARTrackedImageManager>();
        }

        void OnEnable()
        {
          _imageManager.trackedImagesChanged += OnTrackedImagesChanged;

            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged += OnNavStateChanged;
        }
        public void OnStartButton()
        {
//             _imageManager.trackedImagesChanged += OnTrackedImagesChanged;

        //    if (RouteManager.Instance != null)
          //      RouteManager.Instance.OnStateChanged += OnNavStateChanged;
        }
        void OnDisable()
        {
            _imageManager.trackedImagesChanged -= OnTrackedImagesChanged;

            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged -= OnNavStateChanged;
        }

        // ── AR Foundation Callback ────────────────────────────────────────────────
        void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            // New images just detected this frame
            foreach (var trackedImage in args.added)
                HandleTrackedImage(trackedImage);

            // Images whose tracking state updated (e.g. re-acquired after occlusion)
            foreach (var trackedImage in args.updated)
            {
                if (trackedImage.trackingState == TrackingState.Tracking)
                    HandleTrackedImage(trackedImage);
            }
        }

        void HandleTrackedImage(ARTrackedImage trackedImage)
        {
            string imageName = trackedImage.referenceImage.name;

            Log($"ARCore sees image: '{imageName}'  state: {trackedImage.trackingState}");

            // Forward to RouteManager (it decides if this image is the expected one)
            if (!_recognisedThisRound.Contains(imageName))
            {
                _recognisedThisRound.Add(imageName);
                RouteManager.Instance?.OnImageRecognised(imageName);
            }

            // Spawn overlay on the physical image if a prefab is assigned
            SpawnOverlay(trackedImage);
        }

        // ── Overlay ───────────────────────────────────────────────────────────────
        void SpawnOverlay(ARTrackedImage trackedImage)
        {
            if (MarkerOverlayPrefab == null) return;
            if (_overlays.ContainsKey(trackedImage.trackableId)) return;

            var go = Instantiate(MarkerOverlayPrefab,
                                 trackedImage.transform.position,
                                 trackedImage.transform.rotation,
                                 trackedImage.transform);

            _overlays[trackedImage.trackableId] = go;

            if (OverlayLifetimeSeconds > 0f)
                Destroy(go, OverlayLifetimeSeconds);
        }

        // ── Route State Changes ───────────────────────────────────────────────────
        void OnNavStateChanged(RouteManager.NavState state, ARNavigation.Data.NavigationStop _)
        {
            // When a new scan round starts, clear the already-recognised set
            // so the NEXT stop's image can be picked up fresh
            if (state == RouteManager.NavState.WaitingForImage)
            {
                _recognisedThisRound.Clear();
                Log("Image tracker reset — ready for next stop's image.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        void Log(string msg) { if (VerboseLogging) Debug.Log($"[ARImageTracker] {msg}"); }
    }
}
