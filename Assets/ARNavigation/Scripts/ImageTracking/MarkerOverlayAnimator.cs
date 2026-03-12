using System.Collections;
using UnityEngine;

namespace ARNavigation.ImageTracking
{
    /// <summary>
    /// Attach this to the MarkerOverlayPrefab.
    /// Plays a scale-in + glow animation when placed on a recognised image,
    /// then fades out before being destroyed by ARImageTracker.
    ///
    /// SETUP:
    ///   • Create a simple quad in the prefab with a bright/glowing material.
    ///   • Attach this script to the root of that prefab.
    ///   • The prefab will be placed flat on the physical image surface by ARCore.
    /// </summary>
    public class MarkerOverlayAnimator : MonoBehaviour
    {
        [Header("Animation")]
        public float ScaleInDuration   = 0.3f;
        public float HoldDuration      = 1.5f;
        public float FadeOutDuration   = 0.5f;

        [Header("Visuals")]
        [Tooltip("The Renderer whose material alpha will be faded.")]
        public Renderer OverlayRenderer;

        void Start()
        {
            StartCoroutine(PlayAnimation());
        }

        IEnumerator PlayAnimation()
        {
            // ── Scale in ──────────────────────────────────────────────────────────
            transform.localScale = Vector3.zero;
            float t = 0f;
            while (t < ScaleInDuration)
            {
                t += Time.deltaTime;
                float scale = Mathf.SmoothStep(0f, 1f, t / ScaleInDuration);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            transform.localScale = Vector3.one;

            // ── Hold ──────────────────────────────────────────────────────────────
            yield return new WaitForSeconds(HoldDuration);

            // ── Fade out ──────────────────────────────────────────────────────────
            if (OverlayRenderer != null)
            {
                var mat   = OverlayRenderer.material;
                var color = mat.color;
                t = 0f;
                while (t < FadeOutDuration)
                {
                    t += Time.deltaTime;
                    color.a       = Mathf.Lerp(1f, 0f, t / FadeOutDuration);
                    mat.color     = color;
                    yield return null;
                }
            }

            // ARImageTracker will Destroy this GameObject shortly after
        }
    }
}
