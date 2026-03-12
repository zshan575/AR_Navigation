#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARSubsystems;
using ARNavigation.Core;

namespace ARNavigation.Editor
{
    /// <summary>
    /// Editor window that cross-checks the names in your XR Reference Image Library
    /// against the ImageName fields on each NavigationStop in RouteManager.
    ///
    /// Catches typos and case-mismatches BEFORE you build to the device.
    ///
    /// Open via:  Tools > AR Navigation > Validate Image Library
    /// </summary>
    public class ImageLibraryValidator : EditorWindow
    {
        RouteManager             _routeManager;
        XRReferenceImageLibrary  _imageLibrary;

        [MenuItem("Tools/AR Navigation/Validate Image Library")]
        static void Open() => GetWindow<ImageLibraryValidator>("Image Library Validator");

        void OnGUI()
        {
            GUILayout.Label("AR Navigation — Image Library Validator", EditorStyles.boldLabel);
            GUILayout.Space(8);

            _routeManager = (RouteManager)EditorGUILayout.ObjectField(
                "Route Manager", _routeManager, typeof(RouteManager), true);

            _imageLibrary = (XRReferenceImageLibrary)EditorGUILayout.ObjectField(
                "Reference Image Library", _imageLibrary, typeof(XRReferenceImageLibrary), false);

            GUILayout.Space(12);

            if (_routeManager == null || _imageLibrary == null)
            {
                EditorGUILayout.HelpBox("Assign both the RouteManager (from scene) " +
                    "and the XR Reference Image Library asset.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Validate", GUILayout.Height(36)))
                Validate();
        }

        void Validate()
        {
            var stops  = _routeManager.Route?.Stops;
            if (stops == null || stops.Length == 0)
            {
                EditorUtility.DisplayDialog("No Stops", "RouteManager has no stops defined.", "OK");
                return;
            }

            // Build a set of all image names in the library
            var libraryNames = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _imageLibrary.count; i++)
                libraryNames.Add(_imageLibrary[i].name);

            // Check every stop
            var errors   = new System.Text.StringBuilder();
            var warnings = new System.Text.StringBuilder();
            int ok       = 0;

            foreach (var stop in stops)
            {
                if (string.IsNullOrWhiteSpace(stop.ImageName))
                {
                    errors.AppendLine($"  • Stop '{stop.DisplayName}' has an EMPTY ImageName.");
                    continue;
                }

                if (libraryNames.Contains(stop.ImageName))
                {
                    ok++;
                }
                else
                {
                    // Check if it's a case mismatch
                    bool caseMismatch = false;
                    foreach (var libName in libraryNames)
                    {
                        if (string.Equals(libName, stop.ImageName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.AppendLine($"  • Stop '{stop.DisplayName}': ImageName '{stop.ImageName}' " +
                                                $"matches library entry '{libName}' but with different casing. " +
                                                "This works at runtime but fix it for clarity.");
                            caseMismatch = true;
                            ok++;
                            break;
                        }
                    }
                    if (!caseMismatch)
                        errors.AppendLine($"  • Stop '{stop.DisplayName}': ImageName '{stop.ImageName}' " +
                                          "NOT FOUND in the Reference Image Library.");
                }
            }

            // Build report
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Checked {stops.Length} stops against {_imageLibrary.count} library images.\n");
            report.AppendLine($"✓  {ok} matched correctly.");

            if (errors.Length > 0)
            {
                report.AppendLine($"\n✗  ERRORS ({errors.ToString().Trim().Split('\n').Length}):");
                report.Append(errors);
            }
            if (warnings.Length > 0)
            {
                report.AppendLine($"\n⚠  WARNINGS:");
                report.Append(warnings);
            }

            if (errors.Length == 0)
                EditorUtility.DisplayDialog("Validation Passed ✓", report.ToString(), "Great!");
            else
                EditorUtility.DisplayDialog("Validation Failed ✗", report.ToString(), "Fix Issues");

            Debug.Log("[ImageLibraryValidator]\n" + report);
        }
    }
}
#endif
