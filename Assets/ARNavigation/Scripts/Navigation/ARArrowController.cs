using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ARNavigation.Core;
using ARNavigation.Data;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// Spawns the 3D directional arrow into AR space in front of the camera
    /// when navigation begins, and removes it when the user arrives or the
    /// state returns to scanning.
    ///
    /// SETUP:
    ///   • Attach to the ARSessionOrigin GameObject (or any persistent object).
    ///   • Assign ArrowPrefab — a prefab that has CompassNavigator on it.
    ///   • Assign ARCamera (the Main Camera under ARSessionOrigin).
    /// </summary>
    public class ARArrowController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("References")]
        [Tooltip("Prefab for the 3D arrow. Must have CompassNavigator component.")]
        public GameObject ArrowPrefab;

        [Tooltip("The AR camera (Main Camera under ARSessionOrigin).")]
        public Camera ARCamera;

        [Header("Placement")]
        [Tooltip("Distance in front of the camera to place the arrow.")]
        public float SpawnDistance = 1.2f;

        [Tooltip("Height offset below camera eye level (negative = lower).")]
        public float HeightOffset = -0.3f;

        [Tooltip("How quickly the arrow floats into position.")]
        public float FloatSpeed = 3f;

        // ── Private ───────────────────────────────────────────────────────────────
        GameObject _arrowInstance;
        Coroutine  _floatRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        void OnEnable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged += HandleStateChange;
        }

        void OnDisable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged -= HandleStateChange;
        }

        // ── State Handling ────────────────────────────────────────────────────────
        void HandleStateChange(RouteManager.NavState state, NavigationStop stop)
        {
            switch (state)
            {
                case RouteManager.NavState.Navigating:
                    SpawnArrow();
                    break;

                case RouteManager.NavState.WaitingForImage:
                case RouteManager.NavState.Arrived:
                case RouteManager.NavState.RouteComplete:
                case RouteManager.NavState.Idle:
                    DespawnArrow();
                    break;
            }
        }

        // ── Arrow Spawn / Despawn ─────────────────────────────────────────────────
        void SpawnArrow()
        {
            DespawnArrow(); // Clean up any existing arrow

            if (ArrowPrefab == null || ARCamera == null)
            {
                Debug.LogError("[ARArrowController] ArrowPrefab or ARCamera not assigned!");
                return;
            }

            Vector3 spawnPos = ARCamera.transform.position
                             + ARCamera.transform.forward * SpawnDistance
                             + Vector3.up * HeightOffset;

            _arrowInstance = Instantiate(ArrowPrefab, spawnPos, Quaternion.identity);

            // Start the gentle floating animation
            if (_floatRoutine != null) StopCoroutine(_floatRoutine);
            _floatRoutine = StartCoroutine(FloatArrow());
        }

        void DespawnArrow()
        {
            if (_floatRoutine != null) { StopCoroutine(_floatRoutine); _floatRoutine = null; }
            if (_arrowInstance != null) { Destroy(_arrowInstance); _arrowInstance = null; }
        }

        // ── Floating animation — keeps arrow at a fixed offset in front of camera ──
        IEnumerator FloatArrow()
        {
            while (_arrowInstance != null)
            {
                Vector3 targetPos = ARCamera.transform.position
                                  + ARCamera.transform.forward * SpawnDistance
                                  + Vector3.up * HeightOffset;

                _arrowInstance.transform.position = Vector3.Lerp(
                    _arrowInstance.transform.position, targetPos, Time.deltaTime * FloatSpeed);

                yield return null;
            }
        }
    }
}
