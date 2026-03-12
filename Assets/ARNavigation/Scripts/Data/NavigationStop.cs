using UnityEngine;

namespace ARNavigation.Data
{
    /// <summary>
    /// Represents one waypoint in the navigation route.
    ///
    /// Instead of a QR code, each stop is recognised by a physical IMAGE that
    /// ARCore has learned from the XR Reference Image Library you build in Unity.
    ///
    /// ImageName must exactly match the name you gave the image inside the
    /// XR Reference Image Library asset (case-sensitive, no file extension).
    /// </summary>
    [System.Serializable]
    public class NavigationStop
    {
        [Tooltip("Must EXACTLY match the image name inside your XR Reference Image Library. " +
                 "e.g. 'stop_reception'  (case-sensitive, no extension).")]
        public string ImageName;

        [Tooltip("Human-readable label shown in the HUD. e.g. 'Reception Desk'.")]
        public string DisplayName;

        [Tooltip("Walking instruction shown while navigating TO this stop.")]
        [TextArea(2, 4)]
        public string Instruction;

        [Tooltip("Compass bearing (degrees, 0=North, 90=East) the user should face " +
                 "when walking from the PREVIOUS stop toward this one.")]
        [Range(0f, 359f)]
        public float BearingDegrees;

        [Tooltip("Approximate walking distance in metres shown in the HUD.")]
        public float DistanceMetres;
    }

    [System.Serializable]
    public class NavigationRoute
    {
        public string RouteName = "My Route";
        public NavigationStop[] Stops;
    }
}
