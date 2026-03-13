using System;
using UnityEngine;
using ARNavigation.Data;

namespace ARNavigation.Core
{
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance { get; private set; }

        [Header("Route Data")]
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
                Log($"Wrong image. Saw '{imageName}', expected '{expected.ImageName}'");
                OnWrongImageDetected?.Invoke(imageName);
            }
        }

        // Called when user taps "Next Location" on arrived panel
        public void ConfirmArrival()
        {
            if (CurrentState != NavState.Navigating) return;
            CurrentStepIndex++;
            if (CurrentStepIndex >= TotalSteps)
                TransitionTo(NavState.RouteComplete);
        //    else
         //       TransitionTo(NavState.WaitingForImage);
        }
public void CompleteRoute()
        {
             TransitionTo(NavState.RouteComplete);
        }

        public void ResetRoute()
        {
            CurrentStepIndex = 0;
            TransitionTo(NavState.Idle);
        }

        // Called by ARSessionStabilityManager after full AR reset
        public void RequireRescan()
        {
            if (CurrentState == NavState.Navigating)
            {
                Log("RequireRescan — returning to WaitingForImage");
                TransitionTo(NavState.WaitingForImage);
            }
        }

        public void TransitionTo(NavState s)
        {
            CurrentState = s;
            Log($"State -> {s}  (step {CurrentStepIndex}/{TotalSteps})");
            OnStateChanged?.Invoke(s, CurrentStop);
        }

        void Log(string msg) { if (VerboseLogging) Debug.Log($"[RouteManager] {msg}"); }
    }
}
