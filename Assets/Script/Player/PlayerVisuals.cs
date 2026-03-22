using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PlayerVisuals — Cute & Cartoony Robot Edition
//
//  HOW TO USE:
//  Add this to each Player GameObject.
//  Set playerIndex (1–4) in the Inspector.
//  Everything else builds itself.
//
//  EFFECT PALETTE (matches robot body colours):
//  P1 — Red robot     → warm red + pink + white sparkles
//  P2 — Blue/grey     → icy blue + silver + soft white
//  P3 — Teal/green    → mint green + cyan + lime
//  P4 — Extra         → sunny yellow + orange + white
//
//  EFFECTS:
//  - Soft pulsing glow halo beneath the robot (like a shadow glow)
//  - Cute floating bubbles rising off the robot when idle
//  - Bouncy aura ring that squishes and stretches
//  - Speed lines / stars when running fast
//  - TACKLE — big cartoony "!" star burst
//  - IMPACT — confetti-style scatter + screen wobble
//  - KNOCKDOWN — dizzy stars orbiting the robot's head
//  - GET UP — cheerful pop burst upward
//  - Color-matched everything
// ============================================================

public class PlayerVisuals : MonoBehaviour
{
    [Header("Player Identity")]
    [Tooltip("1=Red, 2=Blue, 3=Teal, 4=Yellow")]
    public int playerIndex = 1;

    [Header("Optional — auto-found if blank")]
    public Renderer bodyRenderer;
    public PlayerMovement movement;

    // ── Per-player colour sets ───────────────────────────────
    // Each player gets a primary, secondary, and sparkle colour
    static readonly Color[] PrimaryColors = {
        new Color(1.00f, 0.22f, 0.22f, 1f),   // P1 — red
        new Color(0.35f, 0.65f, 1.00f, 1f),   // P2 — sky blue
        new Color(0.15f, 0.90f, 0.75f, 1f),   // P3 — mint teal
        new Color(1.00f, 0.88f, 0.15f, 1f),   // P4 — sunny yellow
    };
    static readonly Color[] SecondaryColors = {
        new Color(1.00f, 0.55f, 0.70f, 1f),   // P1 — pink
        new Color(0.75f, 0.90f, 1.00f, 1f),   // P2 — ice white-blue
        new Color(0.55f, 1.00f, 0.55f, 1f),   // P3 — lime green
        new Color(1.00f, 0.60f, 0.10f, 1f),   // P4 — orange
    };
    static readonly Color[] SparkleColors = {
        new Color(1.00f, 0.90f, 0.90f, 1f),   // P1 — soft white-pink
        new Color(0.90f, 0.95f, 1.00f, 1f),   // P2 — silver white
        new Color(0.90f, 1.00f, 0.90f, 1f),   // P3 — mint white
        new Color(1.00f, 1.00f, 0.80f, 1f),   // P4 — warm white
    };

    // ── Runtime ──────────────────────────────────────────────
    private Color _primary, _secondary, _sparkle;
    private Rigidbody _rb;

    // Visual objects
    private Transform    _haloParent;
    private MeshRenderer _haloRenderer;
    private Material     _haloMat;
    private Transform    _auraRing;
    private MeshRenderer _auraRingRenderer;
    private Material     _auraRingMat;
    private Transform    _glowParent;

    // Dizzy stars (knockdown)
    private List<(Transform t, MeshRenderer mr, float angle, float speed)> _dizzyStars
        = new List<(Transform, MeshRenderer, float, float)>();
    private bool _isDizzy;

    // General particle pool
    private struct Particle
    {
        public Transform    t;
        public MeshRenderer mr;
        public Vector3      vel;
        public float        born;
        public float        life;
        public Color        col;
        public float        baseSize;
        public bool         isBubble;  // bubbles float up, particles arc down
    }
    private List<Particle> _particles = new List<Particle>();

    // Trail pool
    private struct Trail
    {
        public Transform    t;
        public MeshRenderer mr;
        public float        born;
        public float        life;
        public Color        col;
    }
    private List<Trail> _trails = new List<Trail>();

    // World-space parent for particles that should stay in world
    private Transform _worldParent;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        int idx = Mathf.Clamp(playerIndex - 1, 0, 3);
        _primary   = PrimaryColors[idx];
        _secondary = SecondaryColors[idx];
        _sparkle   = SparkleColors[idx];

        _rb = GetComponent<Rigidbody>();
        if (!bodyRenderer) bodyRenderer = GetComponentInChildren<Renderer>();
        if (!movement)     movement     = GetComponent<PlayerMovement>();

        // World-space parent for effects
        var wp = new GameObject("VFXWorld_P" + playerIndex);
        _worldParent = wp.transform;

        BuildHalo();
        BuildAuraRing();
        BuildDizzyStars();
    }

    void Start()
    {
        StartCoroutine(LoopHalo());
        StartCoroutine(LoopAuraRing());
        StartCoroutine(LoopIdleBubbles());
        StartCoroutine(LoopSpeedStars());
    }

    void Update()
    {
        TickParticles();
        TickTrails();
        if (_isDizzy) TickDizzyStars();
    }

    void OnDestroy()
    {
        if (_worldParent) Destroy(_worldParent.gameObject);
        foreach (var p in _particles) if (p.t) Destroy(p.t.gameObject);
        foreach (var t in _trails)    if (t.t) Destroy(t.t.gameObject);
    }

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void BuildHalo()
    {
        // Soft glowing disc on the ground — like a cartoon character shadow glow
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Halo";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -1.4f, 0f);
        go.transform.localScale    = new Vector3(1.8f, 0.008f, 1.8f);
        Destroy(go.GetComponent<Collider>());

        _haloMat = new Material(UnlitShader());
        Color hc = _primary; hc.a = 0.5f;
        _haloMat.color = hc;

        _haloRenderer = go.GetComponent<MeshRenderer>();
        _haloRenderer.material = _haloMat;
        _haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _haloParent = go.transform;
    }

    void BuildAuraRing()
    {
        // Thin torus-like ring — faked with a flat cylinder just above ground
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "AuraRing";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -1.35f, 0f);
        go.transform.localScale    = new Vector3(2.2f, 0.005f, 2.2f);
        Destroy(go.GetComponent<Collider>());

        _auraRingMat = new Material(UnlitShader());
        Color rc = _secondary; rc.a = 0.35f;
        _auraRingMat.color = rc;

        _auraRingRenderer = go.GetComponent<MeshRenderer>();
        _auraRingRenderer.material = _auraRingMat;
        _auraRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _auraRing = go.transform;
    }

    void BuildDizzyStars()
    {
        // 5 little stars that orbit the head when knocked down
        for (int i = 0; i < 5; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DizzyStar";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.2f;
            go.transform.localScale    = Vector3.one * Random.Range(0.08f, 0.14f);
            Destroy(go.GetComponent<Collider>());

            var mat = new Material(UnlitShader());
            mat.color = i % 2 == 0 ? _primary : _sparkle;
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.SetActive(false);

            float startAngle = (i / 5f) * 360f;
            _dizzyStars.Add((go.transform, mr, startAngle, Random.Range(180f, 280f)));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region AMBIENT LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopHalo()
    {
        while (true)
        {
            float t = Time.time;

            // Cute squish-stretch breathe on the halo
            float breathe = 1f + 0.10f * Mathf.Sin(t * 2.0f);
            if (_haloParent)
                _haloParent.localScale = new Vector3(1.8f * breathe, 0.008f, 1.8f * breathe);

            // Alpha pulse — brighter when moving
            float speed = _rb ? new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude : 0f;
            float baseAlpha = Mathf.Lerp(0.25f, 0.55f, speed / 8f);
            float pulse = baseAlpha + 0.10f * Mathf.Sin(t * 2.0f);
            if (_haloMat)
            {
                Color c = _primary; c.a = pulse;
                _haloMat.color = c;
            }

            yield return null;
        }
    }

    IEnumerator LoopAuraRing()
    {
        while (true)
        {
            float t = Time.time;

            // Spin slowly
            if (_auraRing) _auraRing.Rotate(0f, 60f * Time.deltaTime, 0f);

            // Bouncy scale — cute squish on a different frequency to halo
            float bounce = 1f + 0.07f * Mathf.Sin(t * 2.8f + 1f);
            if (_auraRing)
                _auraRing.localScale = new Vector3(2.2f * bounce, 0.005f, 2.2f * bounce);

            // Pulse alpha
            if (_auraRingMat)
            {
                Color c = _secondary;
                c.a = 0.20f + 0.15f * Mathf.Sin(t * 2.8f + 1f);
                _auraRingMat.color = c;
            }

            yield return null;
        }
    }

    IEnumerator LoopIdleBubbles()
    {
        // Little round bubbles float up off the robot — very cute
        while (true)
        {
            float speed = _rb ? new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude : 0f;

            // Emit more bubbles when idle, fewer when running (running does speed stars instead)
            if (speed < 3f)
            {
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    Vector2 rand = Random.insideUnitCircle * 0.3f;
                    Vector3 pos  = transform.position + new Vector3(rand.x, Random.Range(0.5f, 1.5f), rand.y);
                    Vector3 vel  = new Vector3(
                        Random.Range(-0.3f, 0.3f),
                        Random.Range(1.2f, 2.5f),
                        Random.Range(-0.3f, 0.3f));

                    // Alternate between primary and sparkle for variety
                    Color col = Random.value > 0.5f ? _primary : _sparkle;
                    col.a = 0.8f;
                    float sz = Random.Range(0.05f, 0.13f);
                    SpawnParticle(pos, vel, col, Random.Range(0.6f, 1.2f), sz, true);
                }
            }
            yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));
        }
    }

    IEnumerator LoopSpeedStars()
    {
        // Little star/sparkle streaks when running fast
        while (true)
        {
            float speed = _rb ? new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude : 0f;
            if (speed > 4f)
            {
                // Emit from feet — opposite to movement direction
                Vector3 back = _rb ? -_rb.linearVelocity.normalized : Vector3.back;
                for (int i = 0; i < 2; i++)
                {
                    Vector2 rand = Random.insideUnitCircle * 0.25f;
                    Vector3 pos  = transform.position + new Vector3(rand.x, 0.15f, rand.y);
                    Vector3 vel  = back * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.2f, 0.8f);
                    Color col    = Random.value > 0.5f ? _secondary : _sparkle;
                    col.a = 1f;
                    SpawnParticle(pos, vel, col, Random.Range(0.15f, 0.3f), Random.Range(0.04f, 0.09f), false);
                }

                // Speed trail dot
                SpawnTrail(transform.position + Vector3.up * 0.1f, _primary, 0.18f);
                yield return new WaitForSeconds(0.04f);
                continue;
            }
            yield return new WaitForSeconds(0.06f);
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PUBLIC HOOKS
    // ────────────────────────────────────────────────────────

    public void OnTackleStart()
    {
        StartCoroutine(TackleEffect());
    }

    public void OnTackleImpact(Vector3 hitPoint)
    {
        StartCoroutine(ImpactEffect(hitPoint));
    }

    public void OnKnockdown()
    {
        StartCoroutine(KnockdownEffect());
    }

    public void OnGetUp()
    {
        StartCoroutine(GetUpEffect());
    }

    public void PulseColor(Color col, float duration)
    {
        StartCoroutine(PulseRoutine(col, duration));
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region EFFECT COROUTINES
    // ────────────────────────────────────────────────────────

    IEnumerator TackleEffect()
    {
        // Cute forward burst — like a cartoon dash with speed lines
        int count = 16;
        for (int i = 0; i < count; i++)
        {
            float angle  = (i / (float)count) * Mathf.PI * 2f;
            Vector3 dir  = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle));
            Vector3 pos  = transform.position + Vector3.up * 0.3f;
            Color col    = i % 3 == 0 ? _sparkle : i % 3 == 1 ? _primary : _secondary;
            col.a = 1f;
            SpawnParticle(pos, dir * Random.Range(3f, 7f), col, Random.Range(0.2f, 0.45f),
                Random.Range(0.06f, 0.14f), false);
        }

        // Halo squish — cartoon impact squish
        if (_haloParent)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float p    = t / 0.2f;
                float squish = p < 0.5f
                    ? Mathf.Lerp(1f, 2.2f, p * 2f)       // squash out
                    : Mathf.Lerp(2.2f, 1f, (p - 0.5f) * 2f); // spring back
                _haloParent.localScale = new Vector3(1.8f * squish, 0.008f, 1.8f * squish);
                yield return null;
            }
        }
        else yield break;
    }

    IEnumerator ImpactEffect(Vector3 hitPoint)
    {
        // Confetti-style burst — lots of colourful dots scatter everywhere
        int count = 28;
        Color[] confetti = { _primary, _secondary, _sparkle, Color.white };
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(
                Mathf.Cos(angle),
                Random.Range(0.3f, 1.2f),
                Mathf.Sin(angle));
            Color col = confetti[i % confetti.Length]; col.a = 1f;
            float sz  = Random.Range(0.05f, 0.16f);
            SpawnParticle(hitPoint, dir * Random.Range(4f, 11f), col,
                Random.Range(0.35f, 0.7f), sz, false);
        }

        // Big halo pop
        if (_haloMat && _haloParent)
        {
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                float p = 1f - t / 0.18f;
                _haloParent.localScale = new Vector3(1.8f + 2f * p, 0.008f, 1.8f + 2f * p);
                Color hc = _sparkle; hc.a = 0.6f * p;
                _haloMat.color = hc;
                yield return null;
            }
        }
        else yield break;
    }

    IEnumerator KnockdownEffect()
    {
        // Red stars scatter then dizzy stars orbit head
        Color warnCol = new Color(1f, 0.2f, 0.2f, 1f);

        for (int i = 0; i < 16; i++)
        {
            Vector3 pos = transform.position + Vector3.up * Random.Range(0.5f, 2f);
            Vector3 vel = new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 4f), Random.Range(-3f, 3f));
            SpawnParticle(pos, vel, warnCol, Random.Range(0.4f, 0.8f), Random.Range(0.05f, 0.12f), false);
        }

        // Halo flashes red
        StartCoroutine(PulseHalo(warnCol, 2.0f));

        // Activate dizzy stars
        _isDizzy = true;
        foreach (var (t, mr, ang, spd) in _dizzyStars)
            if (t) t.gameObject.SetActive(true);

        yield return new WaitForSeconds(2.0f);

        // Deactivate dizzy stars
        _isDizzy = false;
        foreach (var (t, mr, ang, spd) in _dizzyStars)
            if (t) t.gameObject.SetActive(false);
    }

    IEnumerator GetUpEffect()
    {
        // Cheerful upward pop — like a cartoon bounce recovery
        Color[] popCols = { _primary, _secondary, _sparkle };
        for (int i = 0; i < 20; i++)
        {
            Vector2 rand = Random.insideUnitCircle * 0.5f;
            Vector3 pos  = transform.position + new Vector3(rand.x, 0.2f, rand.y);
            Vector3 vel  = new Vector3(
                Random.Range(-1.5f, 1.5f),
                Random.Range(3f, 7f),
                Random.Range(-1.5f, 1.5f));
            Color col = popCols[i % popCols.Length]; col.a = 1f;
            SpawnParticle(pos, vel, col, Random.Range(0.4f, 0.8f),
                Random.Range(0.06f, 0.15f), true); // bubbles floating up
        }

        // Halo big pop
        if (_haloParent)
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float p = Mathf.Sin((t / 0.4f) * Mathf.PI);
                _haloParent.localScale = new Vector3(1.8f + 1.5f * p, 0.008f, 1.8f + 1.5f * p);
                if (_haloMat)
                {
                    Color hc = _primary; hc.a = 0.3f + 0.4f * p;
                    _haloMat.color = hc;
                }
                yield return null;
            }
        }
        else yield break;
    }

    IEnumerator PulseRoutine(Color col, float duration)
    {
        // Flash the halo a given colour
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float fade = Mathf.Sin((t / duration) * Mathf.PI);
            if (_haloMat)
            {
                Color c = Color.Lerp(_primary, col, fade);
                c.a = 0.4f + 0.4f * fade;
                _haloMat.color = c;
            }
            yield return null;
        }
    }

    IEnumerator PulseHalo(Color col, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float flash = 0.5f + 0.5f * Mathf.Sin(t * 10f);
            if (_haloMat)
            {
                Color c = Color.Lerp(_primary, col, flash);
                c.a = 0.3f + 0.3f * flash;
                _haloMat.color = c;
            }
            yield return null;
        }
        if (_haloMat) { Color c = _primary; c.a = 0.4f; _haloMat.color = c; }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region DIZZY STARS TICK
    // ────────────────────────────────────────────────────────

    void TickDizzyStars()
    {
        float radius = 0.55f;
        float headY  = 2.2f; // height above transform origin
        for (int i = 0; i < _dizzyStars.Count; i++)
        {
            var (t, mr, angle, speed) = _dizzyStars[i];
            if (!t) continue;

            float newAngle = angle + speed * Time.deltaTime;
            if (newAngle > 360f) newAngle -= 360f;
            _dizzyStars[i] = (t, mr, newAngle, speed);

            float rad = newAngle * Mathf.Deg2Rad;
            t.position = transform.position
                + new Vector3(Mathf.Cos(rad) * radius, headY, Mathf.Sin(rad) * radius);

            // Cute bounce bob
            float bob = Mathf.Sin(Time.time * 8f + i) * 0.08f;
            t.position += Vector3.up * bob;

            // Twinkle scale
            float sc = 0.10f + 0.04f * Mathf.Sin(Time.time * 6f + i * 1.2f);
            t.localScale = Vector3.one * sc;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PROCEDURAL PARTICLES
    // ────────────────────────────────────────────────────────

    void SpawnParticle(Vector3 pos, Vector3 vel, Color col, float life, float size, bool isBubble)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "P";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_worldParent, false);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * size;

        var mat = new Material(UnlitShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _particles.Add(new Particle
        {
            t=go.transform, mr=mr, vel=vel,
            born=Time.time, life=life, col=col,
            baseSize=size, isBubble=isBubble
        });
    }

    void SpawnTrail(Vector3 pos, Color col, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Tr";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_worldParent, false);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.18f;

        var mat = new Material(UnlitShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _trails.Add(new Trail { t=go.transform, mr=mr, born=Time.time, life=life, col=col });
    }

    void TickParticles()
    {
        float now = Time.time;
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            if (!p.t) { _particles.RemoveAt(i); continue; }

            float prog = (now - p.born) / p.life;
            if (prog >= 1f) { Destroy(p.t.gameObject); _particles.RemoveAt(i); continue; }

            if (p.isBubble)
            {
                // Bubbles drift upward with a slight wobble — no gravity pull down
                p.vel += new Vector3(
                    Mathf.Sin(now * 3f + i) * 0.3f,
                    0.1f,
                    Mathf.Cos(now * 2.5f + i) * 0.3f) * Time.deltaTime;
                p.vel *= (1f - 0.8f * Time.deltaTime); // gentle drag
            }
            else
            {
                // Regular particles arc with gravity
                p.vel += Vector3.down * 6f * Time.deltaTime;
            }

            p.t.position += p.vel * Time.deltaTime;
            _particles[i] = p;

            // Bubbles grow slightly then shrink — regular particles just shrink
            float sc;
            if (p.isBubble)
                sc = p.baseSize * (1f + 0.3f * Mathf.Sin(prog * Mathf.PI));
            else
                sc = p.baseSize * Mathf.Lerp(1f, 0f, prog * 0.8f);

            p.t.localScale = Vector3.one * Mathf.Max(sc, 0.001f);

            float alpha = p.isBubble
                ? Mathf.Lerp(0.85f, 0f, prog * prog)
                : Mathf.Lerp(1f, 0f, prog);

            var c = p.col; c.a = alpha;
            p.mr.material.color = c;
        }
    }

    void TickTrails()
    {
        float now = Time.time;
        for (int i = _trails.Count - 1; i >= 0; i--)
        {
            var tr = _trails[i];
            if (!tr.t) { _trails.RemoveAt(i); continue; }

            float prog = (now - tr.born) / tr.life;
            if (prog >= 1f) { Destroy(tr.t.gameObject); _trails.RemoveAt(i); continue; }

            float alpha = Mathf.Lerp(0.6f, 0f, prog);
            float sc    = Mathf.Lerp(0.18f, 0.03f, prog);
            tr.t.localScale = Vector3.one * sc;
            var c = tr.col; c.a = alpha;
            tr.mr.material.color = c;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SHADER HELPERS
    // ────────────────────────────────────────────────────────

    static Shader UnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");
    }

    #endregion
}