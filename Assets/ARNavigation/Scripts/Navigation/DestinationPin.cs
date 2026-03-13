using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavigation.Navigation
{
    /// <summary>
    /// DESTINATION PIN — Full Sci-Fi Animation System
    /// ================================================
    /// Animations included:
    ///   ✓ Bounce / bob up-down
    ///   ✓ Continuous spin
    ///   ✓ Scale pulse (grows + shrinks)
    ///   ✓ Beam of light shooting upward
    ///   ✓ Multiple ripple rings expanding from base
    ///   ✓ Particle sparkle glow (CPU particles, no Particle System component needed)
    ///   ✓ Arrow prefab support with all animations applied
    ///   ✓ Breathing color glow (brightness pulses)
    ///
    /// SETUP:
    ///   1. Create Empty GO → Add DestinationPin script → Save as Prefab
    ///   2. Assign your Arrow Prefab in Inspector (or leave empty for auto-built)
    ///   3. Assign DestinationPin prefab to ARPathLineRenderer → Pin Prefab slot
    /// </summary>
    public class DestinationPin : MonoBehaviour
    {
        // ─── ARROW PREFAB ─────────────────────────────────────────────────────────
        [Header("── Arrow Prefab ──────────────────────")]
        [Tooltip("Your 3D arrow prefab. Leave empty to use auto-built arrow.")]
        public GameObject ArrowPrefab;

        [Tooltip("Scale of the arrow. Start: 0.3")]
        public float ArrowScale = 0.3f;

        [Tooltip("Height above ground. Default: 0.6")]
        public float ArrowHeight = 0.6f;

        // ─── COLORS ───────────────────────────────────────────────────────────────
        [Header("── Colors ───────────────────────────")]
        public Color PrimaryColor = new Color(0f, 0.95f, 0.8f, 1f);      // teal
        public Color SecondaryColor = new Color(0.2f, 0.6f, 1f, 1f);     // blue
        public Color BeamColor = new Color(0f, 1f, 0.85f, 0.6f);

        // ─── BOB ──────────────────────────────────────────────────────────────────
        [Header("── Bob (up/down float) ─────────────")]
        public bool  BobEnabled = true;
        [Range(0f, 0.3f)] public float BobAmount = 0.1f;
        public float BobSpeed  = 1.8f;

        // ─── SPIN ─────────────────────────────────────────────────────────────────
        [Header("── Spin ────────────────────────────")]
        public bool  SpinEnabled = true;
        public float SpinSpeed   = 50f;     // degrees/sec

        // ─── SCALE PULSE ──────────────────────────────────────────────────────────
        [Header("── Scale Pulse ─────────────────────")]
        public bool  ScalePulseEnabled = true;
        [Range(0.8f, 1f)] public float ScaleMin = 0.88f;
        [Range(1f, 1.3f)] public float ScaleMax = 1.12f;
        public float ScalePulseSpeed = 2.2f;

        // ─── BEAM ─────────────────────────────────────────────────────────────────
        [Header("── Light Beam (shoots upward) ───────")]
        public bool  BeamEnabled  = true;
        public float BeamHeight   = 2.5f;
        public float BeamWidth    = 0.06f;
        [Range(0f, 1f)] public float BeamAlpha = 0.55f;

        // ─── RIPPLE RINGS ─────────────────────────────────────────────────────────
        [Header("── Ripple Rings ─────────────────────")]
        public bool  RipplesEnabled  = true;
        [Range(1, 4)] public int RippleCount = 3;
        public float RippleMaxRadius = 0.9f;
        public float RipplePeriod    = 1.6f;    // seconds per full ring cycle

        // ─── SPARKLES ─────────────────────────────────────────────────────────────
        [Header("── Sparkle Particles ───────────────")]
        public bool  SparklesEnabled  = true;
        [Range(3, 20)] public int SparkleCount = 8;
        public float SparkleRadius    = 0.35f;
        public float SparkleSpeed     = 0.9f;
        public float SparkleSize      = 0.04f;

        // ─── BREATHING GLOW ───────────────────────────────────────────────────────
        [Header("── Breathing Glow ───────────────────")]
        public bool  GlowEnabled   = true;
        public float GlowMinAlpha  = 0.4f;
        public float GlowMaxAlpha  = 1.0f;
        public float GlowSpeed     = 1.5f;

        // ─── PRIVATE ──────────────────────────────────────────────────────────────
        GameObject          _arrowInstance;
        Vector3             _arrowBasePos;

        // Beam
        GameObject          _beam;
        Material            _beamMat;

        // Ripple rings
        GameObject[]        _rings;
        Material[]          _ringMats;

        // Sparkle particles (simple spheres)
        GameObject[]        _sparkles;
        Material[]          _sparkleMats;
        float[]             _sparkleAngles;
        float[]             _sparkleSpeeds;
        float[]             _sparklePhases;

        // Ground disc
        GameObject          _disc;
        Material            _discMat;

        // All materials that breathe
        List<Material>      _glowMats = new List<Material>();

        float _bobTimer;
        float _glowTimer;

        // ─── LIFECYCLE ────────────────────────────────────────────────────────────
        void Start()
        {
            BuildGroundDisc();
            if (RipplesEnabled)   BuildRippleRings();
            if (BeamEnabled)      BuildBeam();
            if (SparklesEnabled)  BuildSparkles();

            if (ArrowPrefab != null)
                SpawnArrowPrefab();
            else
                BuildAutoArrow();

            _arrowBasePos = new Vector3(0f, ArrowHeight, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _bobTimer  += dt * BobSpeed;
            _glowTimer += dt * GlowSpeed;

            if (_arrowInstance != null)
            {
                // ── Bob ────────────────────────────────────────────────────────────
                if (BobEnabled)
                {
                    float bob = Mathf.Sin(_bobTimer) * BobAmount;
                    _arrowInstance.transform.localPosition =
                        _arrowBasePos + new Vector3(0f, bob, 0f);
                }

                // ── Spin ───────────────────────────────────────────────────────────
                if (SpinEnabled)
                    _arrowInstance.transform.Rotate(Vector3.up, SpinSpeed * dt, Space.Self);

                // ── Scale Pulse ────────────────────────────────────────────────────
                if (ScalePulseEnabled)
                {
                    float t = (Mathf.Sin(_bobTimer * ScalePulseSpeed * 0.5f) + 1f) * 0.5f;
                    float s = Mathf.Lerp(ScaleMin, ScaleMax, t) * ArrowScale;
                    _arrowInstance.transform.localScale = Vector3.one * s;
                }
            }

            // ── Ripple rings ───────────────────────────────────────────────────────
            if (RipplesEnabled && _rings != null)
                AnimateRipples(dt);

            // ── Beam flicker ───────────────────────────────────────────────────────
            if (BeamEnabled && _beamMat != null)
                AnimateBeam();

            // ── Sparkles orbit ────────────────────────────────────────────────────
            if (SparklesEnabled && _sparkles != null)
                AnimateSparkles(dt);

            // ── Breathing glow ─────────────────────────────────────────────────────
            if (GlowEnabled)
                AnimateGlow();
        }

        // ─── ARROW ────────────────────────────────────────────────────────────────
        void SpawnArrowPrefab()
        {
            _arrowInstance = Instantiate(ArrowPrefab, transform);
            _arrowInstance.transform.localPosition = new Vector3(0f, ArrowHeight, 0f);
            _arrowInstance.transform.localRotation = Quaternion.identity;
            _arrowInstance.transform.localScale    = Vector3.one * ArrowScale;

            // Register all renderers for glow breathing
            foreach (var r in _arrowInstance.GetComponentsInChildren<Renderer>())
                _glowMats.Add(r.material);

            Debug.Log($"[DestinationPin] Arrow prefab '{ArrowPrefab.name}' spawned.");
        }

        void BuildAutoArrow()
        {
            _arrowInstance = new GameObject("AutoArrow");
            _arrowInstance.transform.SetParent(transform, false);
            _arrowInstance.transform.localPosition = new Vector3(0f, ArrowHeight, 0f);
            _arrowInstance.transform.localScale    = Vector3.one * ArrowScale;

            // Shaft
            var shaft = Prim(_arrowInstance, PrimitiveType.Cylinder, PrimaryColor);
            shaft.transform.localPosition = Vector3.zero;
            shaft.transform.localScale    = new Vector3(0.18f, 0.55f, 0.18f);
            _glowMats.Add(shaft.GetComponent<Renderer>().material);

            // Diamond head
            var head = Prim(_arrowInstance, PrimitiveType.Cube, PrimaryColor);
            head.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            head.transform.localScale    = new Vector3(0.38f, 0.38f, 0.38f);
            head.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            _glowMats.Add(head.GetComponent<Renderer>().material);
        }

        // ─── GROUND DISC ──────────────────────────────────────────────────────────
        void BuildGroundDisc()
        {
            _disc = Prim(gameObject, PrimitiveType.Cylinder,
                new Color(PrimaryColor.r, PrimaryColor.g, PrimaryColor.b, 0.4f));
            _disc.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            _disc.transform.localScale    = new Vector3(0.35f, 0.004f, 0.35f);
            _discMat = _disc.GetComponent<Renderer>().material;
            _glowMats.Add(_discMat);
        }

        // ─── RIPPLE RINGS ─────────────────────────────────────────────────────────
        void BuildRippleRings()
        {
            _rings    = new GameObject[RippleCount];
            _ringMats = new Material[RippleCount];

            for (int i = 0; i < RippleCount; i++)
            {
                var ring = Prim(gameObject, PrimitiveType.Cylinder,
                    new Color(PrimaryColor.r, PrimaryColor.g, PrimaryColor.b, 0f));
                ring.transform.localPosition = new Vector3(0f, 0.002f + i * 0.001f, 0f);
                ring.transform.localScale    = new Vector3(0.05f, 0.002f, 0.05f);
                _rings[i]    = ring;
                _ringMats[i] = ring.GetComponent<Renderer>().material;
            }
        }

        void AnimateRipples(float dt)
        {
            float period = RipplePeriod;
            for (int i = 0; i < _rings.Length; i++)
            {
                // Each ring is offset in time so they cascade
                float offset   = (float)i / RippleCount;
                float t        = ((Time.time / period) + offset) % 1f;
                float radius   = Mathf.Lerp(0.05f, RippleMaxRadius, t);
                float alpha    = Mathf.Lerp(0.75f, 0f, t);

                _rings[i].transform.localScale = new Vector3(radius, 0.002f, radius);

                var c = _ringMats[i].color;
                // Alternate rings between primary and secondary color
                Color baseCol = (i % 2 == 0) ? PrimaryColor : SecondaryColor;
                c.r = baseCol.r; c.g = baseCol.g; c.b = baseCol.b;
                c.a = alpha;
                _ringMats[i].color = c;
            }
        }

        // ─── LIGHT BEAM ───────────────────────────────────────────────────────────
        void BuildBeam()
        {
            _beam = Prim(gameObject, PrimitiveType.Cylinder, BeamColor);
            _beam.transform.localPosition = new Vector3(0f, BeamHeight * 0.5f, 0f);
            _beam.transform.localScale    = new Vector3(BeamWidth, BeamHeight * 0.5f, BeamWidth);
            _beamMat = _beam.GetComponent<Renderer>().material;

            // Make beam very transparent (it just glows, doesn't block view)
            var c = _beamMat.color;
            c.a = BeamAlpha;
            _beamMat.color = c;
        }

        void AnimateBeam()
        {
            // Subtle flicker
            float flicker = 0.85f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.15f;
            var c = _beamMat.color;
            c.a   = BeamAlpha * flicker;
            _beamMat.color = c;

            // Beam slowly pulses in width
            float w = BeamWidth * (0.8f + Mathf.Sin(Time.time * 1.2f) * 0.2f);
            _beam.transform.localScale = new Vector3(w, BeamHeight * 0.5f, w);
        }

        // ─── SPARKLE PARTICLES ────────────────────────────────────────────────────
        void BuildSparkles()
        {
            _sparkles      = new GameObject[SparkleCount];
            _sparkleMats   = new Material[SparkleCount];
            _sparkleAngles = new float[SparkleCount];
            _sparkleSpeeds = new float[SparkleCount];
            _sparklePhases = new float[SparkleCount];

            for (int i = 0; i < SparkleCount; i++)
            {
                // Alternate between primary and secondary color
                Color col = (i % 2 == 0) ? PrimaryColor : SecondaryColor;
                col.a = 0.9f;

                var sp = Prim(gameObject, PrimitiveType.Sphere, col);
                float size = SparkleSize * Random.Range(0.6f, 1.4f);
                sp.transform.localScale = Vector3.one * size;

                _sparkles[i]      = sp;
                _sparkleMats[i]   = sp.GetComponent<Renderer>().material;
                _sparkleAngles[i] = (float)i / SparkleCount * 360f;
                _sparkleSpeeds[i] = SparkleSpeed * Random.Range(0.7f, 1.3f);
                _sparklePhases[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        void AnimateSparkles(float dt)
        {
            for (int i = 0; i < _sparkles.Length; i++)
            {
                // Orbit around the pin at varying heights
                _sparkleAngles[i] += _sparkleSpeeds[i] * 90f * dt;

                float rad   = _sparkleAngles[i] * Mathf.Deg2Rad;
                float r     = SparkleRadius * (0.7f + Mathf.Sin(Time.time + _sparklePhases[i]) * 0.3f);
                float h     = ArrowHeight * 0.3f + Mathf.Sin(Time.time * 1.3f + _sparklePhases[i]) * ArrowHeight * 0.4f;

                _sparkles[i].transform.localPosition = new Vector3(
                    Mathf.Cos(rad) * r,
                    h,
                    Mathf.Sin(rad) * r);

                // Twinkle alpha
                float alpha = 0.5f + Mathf.Sin(Time.time * 4f + _sparklePhases[i]) * 0.5f;
                var c = _sparkleMats[i].color;
                c.a   = alpha;
                _sparkleMats[i].color = c;
            }
        }

        // ─── BREATHING GLOW ───────────────────────────────────────────────────────
        void AnimateGlow()
        {
            float t     = (Mathf.Sin(_glowTimer) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(GlowMinAlpha, GlowMaxAlpha, t);

            foreach (var mat in _glowMats)
            {
                if (mat == null) continue;
                var c = mat.color;
                // Shift color slightly between primary and secondary during glow
                c.r = Mathf.Lerp(PrimaryColor.r, SecondaryColor.r, t * 0.3f);
                c.g = Mathf.Lerp(PrimaryColor.g, SecondaryColor.g, t * 0.3f);
                c.b = Mathf.Lerp(PrimaryColor.b, SecondaryColor.b, t * 0.3f);
                c.a = alpha;
                mat.color = c;
            }
        }

        // ─── UTILITY: make primitive with transparent material ────────────────────
        GameObject Prim(GameObject parent, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent.transform, false);

            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode",    3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue      = 3000;
            mat.color            = color;
            mat.enableInstancing = true;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }
    }
}
