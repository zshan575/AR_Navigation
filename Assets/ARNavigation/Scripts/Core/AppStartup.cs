using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using ARNavigation.Core;

namespace ARNavigation.Core
{
    /// <summary>
    /// Entry-point. Requests Android permissions, then starts navigation.
    /// No ZXing or QR scanner — image tracking is handled by ARImageTracker.
    /// </summary>
    public class AppStartup : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Auto-start navigation after permissions are granted.")]
        public bool AutoStart = true;

        IEnumerator Start()
        {
            yield return RequestPermissions();
            if (AutoStart && RouteManager.Instance != null)
                RouteManager.Instance.StartNavigation();
        }

        IEnumerator RequestPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                yield return new WaitForSeconds(0.5f);
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                    yield return null;
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(0.5f);
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                    yield return null;
            }
#else
            yield return null;
#endif
        }

        public void OnStartButtonPressed() => RouteManager.Instance?.StartNavigation();
        public void OnResetButtonPressed() => RouteManager.Instance?.ResetRoute();
    }
}
