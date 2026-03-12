using System.Collections;
using UnityEngine;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// The glowing destination pin placed at the far end of the AR path line.
    /// Attach to the PinPrefab root. Optionally auto-builds a simple pin shape
    /// if no child mesh is found, so it works out-of-the-box with no art assets.
    ///
    /// SETUP OPTIONS:
    ///   A) Auto-built (no art needed):
    ///      • Create an empty GameObject, attach this script, save as PinPrefab.
    ///      • Script auto-generates a cone + disc + pulse ring using basic meshes.
    ///
    ///   B) Custom art:
    ///      • Design your pin model, add it as a child of the prefab root.
    ///      • Attach this script to the root for the bob + pulse animations.
    ///      • Set AutoBuild = false.
    /// </summary>
    public class DestinationPin : MonoBehaviour
    {
        [Header("Auto Build")]
        [Tooltip("If true and no child renderers found, auto-creates a simple pin shape.")]
        public bool AutoBuild = true;

        [Header("Colors")]
        public Color PinColor  = new Color(0f, 0.95f, 0.8f, 1f);   // teal
        public Color RingColor = new Color(0f, 0.95f, 0.8f, 0.4f);

        [Header("Pulse Ring")]
        public float RingMaxScale    = 2.5f;
        public float RingPulsePeriod = 1.2f;   // seconds per pulse

        // ── Private ───────────────────────────────────────────────────────────────
        GameObject _ring;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        void Start()
        {
            if (AutoBuild && GetComponentInChildren<MeshRenderer>() == null)
                BuildDefaultPin();

            if (_ring != null)
                StartCoroutine(PulseRing());
        }

        // ── Auto-build a simple pin from primitive meshes ─────────────────────────
        void BuildDefaultPin()
        {
            // Vertical pole
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(transform, false);
            pole.transform.localScale    = new Vector3(0.04f, 0.25f, 0.04f);
            pole.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            DestroyImmediate(pole.GetComponent<CapsuleCollider>());
            SetMaterialColor(pole, PinColor);

            // Sphere head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(transform, false);
            head.transform.localScale    = new Vector3(0.14f, 0.14f, 0.14f);
            head.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            DestroyImmediate(head.GetComponent<SphereCollider>());
            SetMaterialColor(head, PinColor);

            // Ground disc
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(transform, false);
            disc.transform.localScale    = new Vector3(0.25f, 0.005f, 0.25f);
            disc.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            DestroyImmediate(disc.GetComponent<CapsuleCollider>());
            SetMaterialColor(disc, new Color(PinColor.r, PinColor.g, PinColor.b, 0.5f));

            // Pulse ring (flat cylinder, scaled by coroutine)
            _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ring.transform.SetParent(transform, false);
            _ring.transform.localScale    = new Vector3(0.3f, 0.002f, 0.3f);
            _ring.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            DestroyImmediate(_ring.GetComponent<CapsuleCollider>());
            SetMaterialColor(_ring, RingColor);
        }

        // ── Pulse ring expands outward and fades ──────────────────────────────────
        IEnumerator PulseRing()
        {
            var mat = _ring.GetComponent<Renderer>().material;

            while (true)
            {
                float t = 0f;
                while (t < RingPulsePeriod)
                {
                    t += Time.deltaTime;
                    float progress = t / RingPulsePeriod;

                    float scale = Mathf.Lerp(0.3f, RingMaxScale * 0.3f, progress);
                    _ring.transform.localScale = new Vector3(scale, 0.002f, scale);

                    var c  = mat.color;
                    c.a    = Mathf.Lerp(0.6f, 0f, progress);
                    mat.color = c;

                    yield return null;
                }
            }
        }

        // ── Util ──────────────────────────────────────────────────────────────────
        static void SetMaterialColor(GameObject go, Color color)
        {
            var r   = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));

            // Enable transparency
            mat.SetFloat("_Mode", 3);   // Transparent
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            mat.color          = color;
            mat.enableInstancing = true;
            r.material         = mat;
        }
    }
}
