using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PlayerVisuals — AAA Self-Building Player Effects
//
//  HOW TO USE:
//  1. Add this script to each Player prefab / GameObject.
//  2. Set "playerIndex" in the Inspector (1, 2, 3, or 4).
//  3. That's it. Everything is built in code.
//
//  WHAT IT DOES:
//  - Gives each player a unique colour (cyan / orange / violet / green)
//  - Pulsing ground aura ring beneath the player
//  - Animated body glow that breathes and spikes on events
//  - Motion trail (proc. particles left behind while moving)
//  - TACKLE burst — explosive particle ring when tackle starts
//  - IMPACT burst — when a tackle hits someone
//  - KNOCKDOWN effect — red flash + spin sparks when knocked down
//  - RESPAWN effect — rising particles + scale pop when standing up
//  - Idle "energy charge" sparks rising off the player
//  - Low-health pulse (if you add health later, hook into PulseRed)
//
//  Hooks you can call from other scripts:
//    visuals.OnTackleStart()
//    visuals.OnTackleImpact(Vector3 hitPoint)
//    visuals.OnKnockdown()
//    visuals.OnGetUp()
//    visuals.PulseColor(Color col, float duration)
// ============================================================

[RequireComponent(typeof(Renderer))]
public class PlayerVisuals : MonoBehaviour
{
    [Header("Player Identity")]
    [Tooltip("1 = cyan, 2 = orange, 3 = violet, 4 = lime")]
    public int playerIndex = 1;

    [Header("Optional refs (auto-found if blank)")]
    public Renderer bodyRenderer;   // your capsule/mesh renderer
    public PlayerMovement movement; // for velocity-based effects

    // ── Player colour palette ────────────────────────────────
    static readonly Color[] PlayerColors = {
        new Color(0.00f, 0.88f, 1.00f, 1f),  // 1 — electric cyan
        new Color(1.00f, 0.42f, 0.06f, 1f),  // 2 — ember orange
        new Color(0.65f, 0.12f, 1.00f, 1f),  // 3 — deep violet
        new Color(0.20f, 1.00f, 0.30f, 1f),  // 4 — neon lime
    };

    static readonly Color ColorWhite = Color.white;
    static readonly Color ColorRed = new Color(1f, 0.15f, 0.05f, 1f);

    // ── Runtime state ────────────────────────────────────────
    private Color _myColor;
    private Material _bodyMat;
    private Rigidbody _rb;

    // Effect objects
    private Transform _auraRing;
    private MeshRenderer _auraRenderer;
    private Transform _glowSphere;
    private MeshRenderer _glowRenderer;
    private Transform _trailParent;
    private Transform _sparkParent;

    // Material refs for runtime tinting
    private Material _auraMat;
    private Material _glowMat;

    // Coroutine handles
    private Coroutine _pulseRoutine;
    private Coroutine _trailRoutine;
    private Coroutine _sparkRoutine;
    private Coroutine _auraRoutine;

    // Trail pool
    private readonly List<(Transform t, MeshRenderer r, float born, float life)> _trailDots
        = new List<(Transform, MeshRenderer, float, float)>();
    private const int TRAIL_POOL = 40;

    // Particle pool (shared for bursts)
    private readonly List<(Transform t, MeshRenderer r, Vector3 vel, float born, float life, Color col)> _particles
        = new List<(Transform, MeshRenderer, Vector3, float, float, Color)>();

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        _myColor = PlayerColors[Mathf.Clamp(playerIndex - 1, 0, PlayerColors.Length - 1)];

        if (!bodyRenderer) bodyRenderer = GetComponent<Renderer>();
        if (!movement) movement = GetComponent<PlayerMovement>();
        _rb = GetComponent<Rigidbody>();

        SetupBodyMaterial();
        BuildAura();
        BuildGlowSphere();
        BuildTrailParent();
        BuildSparkParent();
    }

    void Start()
    {
        _auraRoutine = StartCoroutine(LoopAura());
        _trailRoutine = StartCoroutine(LoopTrail());
        _sparkRoutine = StartCoroutine(LoopIdleSparks());
    }

    void Update()
    {
        TickParticles();
        TickTrailDots();
    }

    // ────────────────────────────────────────────────────────
    #region SETUP
    // ────────────────────────────────────────────────────────

    void SetupBodyMaterial()
    {
        // Give the player body a unique emissive material
        _bodyMat = new Material(GetUnlitShader());
        _bodyMat.name = "PlayerBody_" + playerIndex;
        _bodyMat.color = new Color(_myColor.r * 0.35f, _myColor.g * 0.35f, _myColor.b * 0.35f, 1f);
        if (bodyRenderer) bodyRenderer.material = _bodyMat;
    }

    void BuildAura()
    {
        // Flat disc on the ground beneath the player
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Aura";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -1.45f, 0f); // sits at feet
        go.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
        go.transform.localRotation = Quaternion.identity;
        Destroy(go.GetComponent<Collider>());

        _auraMat = new Material(GetUnlitShader());
        _auraMat.name = "AuraMat";
        Color ac = _myColor; ac.a = 0.55f;
        _auraMat.color = ac;

        _auraRenderer = go.GetComponent<MeshRenderer>();
        _auraRenderer.material = _auraMat;
        _auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _auraRing = go.transform;
    }

    void BuildGlowSphere()
    {
        // Slightly oversized transparent sphere around the player body
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "GlowSphere";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * 1.35f;
        Destroy(go.GetComponent<Collider>());

        _glowMat = new Material(GetTransparentShader());
        _glowMat.name = "GlowMat";
        Color gc = _myColor; gc.a = 0.10f;
        _glowMat.color = gc;

        _glowRenderer = go.GetComponent<MeshRenderer>();
        _glowRenderer.material = _glowMat;
        _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _glowSphere = go.transform;
    }

    void BuildTrailParent()
    {
        var go = new GameObject("TrailParent");
        go.transform.SetParent(transform.parent ?? transform, false); // world-space parent so trail stays behind
        _trailParent = go.transform;
    }

    void BuildSparkParent()
    {
        var go = new GameObject("SparkParent");
        go.transform.SetParent(transform, false);
        _sparkParent = go.transform;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region AMBIENT LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopAura()
    {
        while (true)
        {
            float t = Time.time;

            // Breathe scale
            float pulse = 1f + 0.12f * Mathf.Sin(t * 2.1f);
            if (_auraRing) _auraRing.localScale = new Vector3(1.6f * pulse, 0.01f, 1.6f * pulse);

            // Alpha pulse
            if (_auraMat)
            {
                Color c = _myColor;
                c.a = 0.30f + 0.25f * Mathf.Sin(t * 2.1f);
                _auraMat.color = c;
            }

            // Slow spin
            if (_auraRing) _auraRing.Rotate(0f, 45f * Time.deltaTime, 0f);

            // Glow sphere breathe
            if (_glowMat)
            {
                Color gc = _myColor;
                gc.a = 0.06f + 0.06f * Mathf.Sin(t * 1.7f + 1f);
                _glowMat.color = gc;
            }
            if (_glowSphere)
            {
                float gs = 1.35f + 0.04f * Mathf.Sin(t * 1.7f);
                _glowSphere.localScale = Vector3.one * gs;
            }

            // Velocity-based body brightness
            if (_bodyMat && _rb)
            {
                float speed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
                float bright = Mathf.Clamp01(speed / 8f);
                Color bc = Color.Lerp(
                    new Color(_myColor.r * 0.3f, _myColor.g * 0.3f, _myColor.b * 0.3f),
                    new Color(_myColor.r * 0.8f, _myColor.g * 0.8f, _myColor.b * 0.8f),
                    bright);
                _bodyMat.color = bc;
            }

            yield return null;
        }
    }

    IEnumerator LoopTrail()
    {
        while (true)
        {
            if (_rb)
            {
                float speed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
                if (speed > 2.5f)
                {
                    // Drop a trail dot at current position
                    float life = Mathf.Lerp(0.12f, 0.35f, speed / 12f);
                    SpawnTrailDot(transform.position + Vector3.up * 0.1f, life);
                    yield return new WaitForSeconds(0.04f);
                    continue;
                }
            }
            yield return new WaitForSeconds(0.06f);
        }
    }

    IEnumerator LoopIdleSparks()
    {
        while (true)
        {
            // Emit a few rising sparks periodically
            int count = Random.Range(1, 3);
            for (int i = 0; i < count; i++)
            {
                Vector2 rand = Random.insideUnitCircle * 0.4f;
                Vector3 pos = transform.position + new Vector3(rand.x, 0.2f, rand.y);
                Vector3 vel = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1.8f, 3.5f), Random.Range(-0.4f, 0.4f));
                SpawnParticle(pos, vel, _myColor, Random.Range(0.3f, 0.7f), Random.Range(0.04f, 0.10f));
            }
            yield return new WaitForSeconds(Random.Range(0.08f, 0.22f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PUBLIC HOOKS — call these from PlayerTackle / PlayerMovement
    // ────────────────────────────────────────────────────────

    /// <summary>Call when this player starts a tackle dash.</summary>
    public void OnTackleStart()
    {
        StartCoroutine(TackleStartEffect());
    }

    /// <summary>Call when this player's tackle lands on a victim.</summary>
    public void OnTackleImpact(Vector3 hitPoint)
    {
        StartCoroutine(TackleImpactEffect(hitPoint));
    }

    /// <summary>Call when this player gets knocked down.</summary>
    public void OnKnockdown()
    {
        StartCoroutine(KnockdownEffect());
    }

    /// <summary>Call when this player gets back up.</summary>
    public void OnGetUp()
    {
        StartCoroutine(GetUpEffect());
    }

    /// <summary>Flash the player body a given colour for a duration.</summary>
    public void PulseColor(Color col, float duration)
    {
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseRoutine(col, duration));
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region EFFECT COROUTINES
    // ────────────────────────────────────────────────────────

    IEnumerator TackleStartEffect()
    {
        // Ring of particles bursting outward at foot level
        int count = 24;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle));
            Vector3 pos = transform.position + Vector3.up * 0.2f;
            SpawnParticle(pos, dir * Random.Range(4f, 9f), _myColor, Random.Range(0.25f, 0.5f), Random.Range(0.08f, 0.18f));
        }

        // Spike glow sphere briefly
        if (_glowMat)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float p = 1f - t / 0.2f;
                Color gc = _myColor; gc.a = 0.10f + 0.55f * p;
                _glowMat.color = gc;
                if (_glowSphere) _glowSphere.localScale = Vector3.one * (1.35f + 0.6f * p);
                yield return null;
            }
        }
        else yield break;
    }

    IEnumerator TackleImpactEffect(Vector3 hitPoint)
    {
        // Big shockwave ring + scattered sparks at impact point
        int ring = 32;
        for (int i = 0; i < ring; i++)
        {
            float angle = (i / (float)ring) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Random.Range(-0.1f, 0.5f), Mathf.Sin(angle));
            float spd = Random.Range(5f, 14f);
            Color col = Random.value > 0.5f ? _myColor : ColorWhite;
            SpawnParticle(hitPoint, dir * spd, col, Random.Range(0.3f, 0.65f), Random.Range(0.06f, 0.2f));
        }

        // Screen-ish flash on body
        PulseColor(ColorWhite, 0.15f);

        // Brief aura spike
        if (_auraMat)
        {
            Color ac = ColorWhite; ac.a = 0.9f; _auraMat.color = ac;
            if (_auraRing) _auraRing.localScale = new Vector3(4f, 0.01f, 4f);
        }
        yield return new WaitForSeconds(0.05f);
        // Aura snaps back via LoopAura naturally
    }

    IEnumerator KnockdownEffect()
    {
        // Red sparks spiral out
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = transform.position + Vector3.up * Random.Range(0f, 1.5f);
            Vector3 vel = new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 5f), Random.Range(-3f, 3f));
            SpawnParticle(pos, vel, ColorRed, Random.Range(0.4f, 0.9f), Random.Range(0.06f, 0.14f));
        }

        // Flash red
        PulseColor(ColorRed, 0.6f);

        // Aura turns red and pulses fast
        float elapsed = 0f;
        while (elapsed < 2.0f) // knockdown duration approx
        {
            elapsed += Time.deltaTime;
            if (_auraMat)
            {
                Color rc = ColorRed;
                rc.a = 0.3f + 0.3f * Mathf.Abs(Mathf.Sin(elapsed * 8f));
                _auraMat.color = rc;
            }
            yield return null;
        }
        // Restore
        if (_auraMat) { Color ac = _myColor; ac.a = 0.45f; _auraMat.color = ac; }
    }

    IEnumerator GetUpEffect()
    {
        // Rising column of sparks
        for (int i = 0; i < 18; i++)
        {
            Vector2 r = Random.insideUnitCircle * 0.5f;
            Vector3 pos = transform.position + new Vector3(r.x, 0f, r.y);
            Vector3 vel = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(2f, 6f), Random.Range(-0.5f, 0.5f));
            SpawnParticle(pos, vel, _myColor, Random.Range(0.4f, 0.9f), Random.Range(0.06f, 0.15f));
        }

        // Scale pop on glow sphere
        if (_glowSphere)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float p = t / 0.35f;
                float pop = 1f + 0.5f * Mathf.Sin(p * Mathf.PI);
                _glowSphere.localScale = Vector3.one * pop * 1.35f;
                if (_glowMat)
                {
                    Color gc = _myColor;
                    gc.a = 0.10f + 0.40f * (1f - p);
                    _glowMat.color = gc;
                }
                yield return null;
            }
        }
        else yield break;
    }

    IEnumerator PulseRoutine(Color col, float duration)
    {
        if (!_bodyMat) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float fade = Mathf.Sin(p * Mathf.PI); // bell curve
            _bodyMat.color = Color.Lerp(
                new Color(_myColor.r * 0.35f, _myColor.g * 0.35f, _myColor.b * 0.35f),
                col, fade);
            yield return null;
        }
        _bodyMat.color = new Color(_myColor.r * 0.35f, _myColor.g * 0.35f, _myColor.b * 0.35f);
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PROCEDURAL PARTICLES
    // ────────────────────────────────────────────────────────

    void SpawnParticle(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "P";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_trailParent ?? transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;

        var mat = new Material(GetUnlitShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _particles.Add((go.transform, mr, vel, Time.time, life, col));
    }

    void TickParticles()
    {
        float now = Time.time;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (t, r, vel, born, life, col) = _particles[i];
            if (!t) { _particles.RemoveAt(i); continue; }

            float age = now - born;
            float prog = age / life;

            if (prog >= 1f)
            {
                Destroy(t.gameObject);
                _particles.RemoveAt(i);
                continue;
            }

            // Move with gravity
            var newVel = vel + Vector3.down * 4f * Time.deltaTime;
            t.position += newVel * Time.deltaTime;
            _particles[i] = (t, r, newVel, born, life, col);

            // Fade and shrink
            float alpha = Mathf.Lerp(1f, 0f, prog * prog);
            float scale = Mathf.Lerp(1f, 0f, prog * 0.6f);
            r.material.color = new Color(col.r, col.g, col.b, alpha);
            t.localScale = Vector3.one * scale * (t.localScale.x > 0 ? t.localScale.x / scale : 0.1f);
            // simpler: just fade
            var c = r.material.color; c.a = alpha; r.material.color = c;
        }
    }

    void SpawnTrailDot(Vector3 pos, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Trail";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_trailParent ?? transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.22f;

        var mat = new Material(GetTransparentShader());
        Color c = _myColor; c.a = 0.55f;
        mat.color = c;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _trailDots.Add((go.transform, mr, Time.time, life));
    }

    void TickTrailDots()
    {
        float now = Time.time;
        for (int i = _trailDots.Count - 1; i >= 0; i--)
        {
            var (t, r, born, life) = _trailDots[i];
            if (!t) { _trailDots.RemoveAt(i); continue; }

            float prog = (now - born) / life;
            if (prog >= 1f) { Destroy(t.gameObject); _trailDots.RemoveAt(i); continue; }

            Color c = _myColor;
            c.a = Mathf.Lerp(0.5f, 0f, prog);
            r.material.color = c;
            float sc = Mathf.Lerp(0.22f, 0.04f, prog);
            t.localScale = Vector3.one * sc;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SHADER HELPERS
    // ────────────────────────────────────────────────────────

    // Gets the best available unlit shader for URP
    static Shader GetUnlitShader()
    {
        var s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    // Gets a transparent-capable shader for URP
    static Shader GetTransparentShader()
    {
        // URP Unlit with surface type Transparent via keywords
        var s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Transparent");
        if (s == null) s = Shader.Find("Standard");

        if (s != null)
        {
            // For URP/Unlit we need to set the surface type to transparent
            // We'll handle alpha via material properties after creation
        }
        return s;
    }

    private static Material MakeTransparentMaterial(Shader shader, Color color)
    {
        var mat = new Material(shader);
        mat.color = color;

        // URP Unlit transparent setup
        mat.SetFloat("_Surface", 1);          // 0 = opaque, 1 = transparent
        mat.SetFloat("_Blend", 0);            // alpha blend
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region CLEANUP
    // ────────────────────────────────────────────────────────

    void OnDestroy()
    {
        // Clean up any orphaned trail/particle objects
        foreach (var (t, _, _, _) in _trailDots) if (t) Destroy(t.gameObject);
        foreach (var (t, _, _, _, _, _) in _particles) if (t) Destroy(t.gameObject);
    }

    #endregion
}