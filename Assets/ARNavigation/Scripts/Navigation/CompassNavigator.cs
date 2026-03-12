using UnityEngine;
using ARNavigation.Core;
using ARNavigation.Data;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// Reads the device compass heading and rotates the AR directional arrow
    /// so it always points toward the current stop's bearing.
    ///
    /// SETUP:
    ///   • Attach to the Arrow GameObject (the one with your arrow mesh/sprite).
    ///   • Works in world-space — the arrow rotates around its Y axis.
    ///   • Requires Location Services permission on Android.
    /// </summary>
    public class CompassNavigator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Arrow Rotation")]
        [Tooltip("Smoothing speed for the arrow rotation. Higher = snappier.")]
        [Range(1f, 20f)]
        public float RotationSmoothing = 5f;

        [Tooltip("Axis around which the arrow rotates. Y for horizontal plane.")]
        public Vector3 RotationAxis = Vector3.up;

        [Header("Compass Calibration")]
        [Tooltip("Magnetic declination offset for your region (degrees). "
               + "Find your value at ngdc.noaa.gov/geomag/calculators/magcalc.shtml")]
        public float MagneticDeclinationOffset = 0f;

        // ── Private ───────────────────────────────────────────────────────────────
        float _targetBearing;     // degrees: where we want to point
        bool  _active;
        float _currentAngle;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        void OnEnable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged += HandleStateChange;

            Input.compass.enabled = true;
            Input.location.Start();
        }

        void OnDisable()
        {
            if (RouteManager.Instance != null)
                RouteManager.Instance.OnStateChanged -= HandleStateChange;

            Input.location.Stop();
        }

        void Update()
        {
            if (!_active) return;

            // Device compass gives magnetic north heading (0=North, 90=East …)
            float compassHeading = Input.compass.trueHeading + MagneticDeclinationOffset;

            // Angle the arrow needs to rotate relative to the device's forward
            float angleDiff = _targetBearing - compassHeading;

            // Normalise to [-180, 180]
            while (angleDiff >  180f) angleDiff -= 360f;
            while (angleDiff < -180f) angleDiff += 360f;

            _currentAngle = Mathf.LerpAngle(_currentAngle, angleDiff, Time.deltaTime * RotationSmoothing);
            transform.localRotation = Quaternion.AngleAxis(_currentAngle, RotationAxis);
        }

        // ── Route Events ──────────────────────────────────────────────────────────
        void HandleStateChange(RouteManager.NavState state, NavigationStop stop)
        {
            switch (state)
            {
                case RouteManager.NavState.Navigating:
                    _targetBearing = stop?.BearingDegrees ?? 0f;
                    _active = true;
                    gameObject.SetActive(true);
                    break;

                case RouteManager.NavState.WaitingForImage:
                case RouteManager.NavState.RouteComplete:
                case RouteManager.NavState.Idle:
                    _active = false;
                    gameObject.SetActive(false);
                    break;
            }
        }
    }
}
