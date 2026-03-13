using System;
using UnityEngine;
using ARNavigation.Data;

namespace ARNavigation.Core
{
    /// <summary>
    /// Central singleton owning route data and navigation state.
    /// Recognition is now driven by AR Foundation Image Tracking — no QR/ZXing needed.
    /// </summary>
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }

        [Header("Route Data")]
        [Tooltip("Define stops in order. Each stop's ImageName must match an entry in the XR Reference Image Library.")]
        public NavigationRoute Route = new NavigationRoute();

        [Header("Debug")]
        public bool VerboseLogging = true;

        public enum NavState { Idle, WaitingForImage, Navigating, Arrived, RouteComplete }

        public NavState       CurrentState     { get; private set; } = NavState.Idle;
        public int            CurrentStepIndex { get; private set; } = -1;
        public int            TotalSteps       => Route.Stops?.Length ?? 0;
        public NavigationStop CurrentStop      =>
            (CurrentStepIndex >= 0 && CurrentStepIndex < TotalSteps)
                ? Route.Stops[CurrentStepIndex] : null;

        public event Action<NavState, NavigationStop> OnStateChanged;
        public event Action<string>                   OnWrongImageDetected;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartNavigation()
        {
            if (TotalSteps == 0) { Debug.LogError("[RouteManager] No stops defined!"); return; }
            CurrentStepIndex = 0;
            TransitionTo(NavState.WaitingForImage);
        }

        /// <summary>Called by ARImageTracker when ARCore recognises a tracked image.</summary>
        public void OnImageRecognised(string imageName)
        {
            if (CurrentState != NavState.WaitingForImage) return;
            var expected = Route.Stops[CurrentStepIndex];
            if (string.Equals(imageName.Trim(), expected.ImageName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Log($"Image matched stop [{CurrentStepIndex}] '{expected.DisplayName}'");
                TransitionTo(NavState.Navigating);
            }
            else
            {
                Log($"Image mismatch. Saw '{imageName}', expected '{expected.ImageName}'");
                OnWrongImageDetected?.Invoke(imageName);
            }
        }

        public void ConfirmArrival()
        {
            if (CurrentState != NavState.Navigating) return;
            TransitionTo(NavState.Arrived);
            CurrentStepIndex++;
            TransitionTo(CurrentStepIndex >= TotalSteps ? NavState.RouteComplete : NavState.WaitingForImage);
        }

        public void ResetRoute()
        {
            CurrentStepIndex = 0;
            TransitionTo(NavState.Idle);
        }

        void TransitionTo(NavState s)
        {
            CurrentState = s;
            Log($"State -> {s}  (step {CurrentStepIndex}/{TotalSteps})");
            OnStateChanged?.Invoke(s, CurrentStop);
        }

        void Log(string msg) { if (VerboseLogging) Debug.Log($"[RouteManager] {msg}"); }

         // Called by ARSessionStabilityManager after full AR reset
        public void RequireRescan()
        {
            if (CurrentState == NavState.Navigating)
            {
                Log("RequireRescan called — returning to WaitingForImage");
                TransitionTo(NavState.WaitingForImage);
            }
        }
    }
}

