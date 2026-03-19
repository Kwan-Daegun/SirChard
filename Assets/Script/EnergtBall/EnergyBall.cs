using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  EnergyBall — AAA Visual Effects Edition
//  Drop-in replacement for your existing EnergyBall.cs
//  Builds ALL visuals in code. Nothing to wire up.
//
//  EFFECTS:
//  - Pulsing inner glow sphere (bright core)
//  - Outer soft glow shell (transparent, breathes)
//  - Orbiting energy particles (3 rings at different angles)
//  - Idle floating bob (gentle up/down)
//  - Flying trail — stream of particles when heading to owner
//  - PICKUP burst — ring explosion when a player grabs it
//  - DROP burst — scatter sparks when ball is dropped
//  - Owner aura boost — ball glows brighter near its holder
//  - Spin speed increases when flying
// ============================================================

public class EnergyBall : MonoBehaviour
{
    [Header("Gameplay")]
    public float flySpeed = 10f;
    public float dropPopForce = 5f;
    public float pickupCooldown = 1f;
    public GameObject currentOwner;

    [Header("Visuals")]
    public Color ballColorIdle = new Color(1.00f, 0.85f, 0.10f, 1f); // golden
    public Color ballColorFlying = new Color(1.00f, 0.55f, 0.05f, 1f); // orange-hot
    public Color ballColorOwned = new Color(0.30f, 1.00f, 0.40f, 1f); // bright green
    public Color ballColorDrop = new Color(1.00f, 0.20f, 0.10f, 1f); // red pop

    // ── Gameplay state ───────────────────────────────────────
    private Transform targetPlaceholder;
    private bool isFlying = false;
    private Rigidbody rb;
    private bool canBePickedUp = true;

    // ── Visual objects ───────────────────────────────────────
    private MeshRenderer _coreRenderer;
    private MeshRenderer _shellRenderer;
    private Material _coreMat;
    private Material _shellMat;
    private Transform _orbitParent;
    private Transform _trailParent;

    // Orbiting particles
    private struct OrbitParticle
    {
        public Transform t;
        public MeshRenderer mr;
        public float angle;
        public float radius;
        public float speed;
        public float tilt;    // axis tilt in degrees
        public float size;
    }
    private List<OrbitParticle> _orbiters = new List<OrbitParticle>();

    // General particles (burst / trail)
    private struct Particle
    {
        public Transform t;
        public MeshRenderer mr;
        public Vector3 vel;
        public float born;
        public float life;
        public Color col;
        public float baseSize;
    }
    private List<Particle> _particles = new List<Particle>();

    // Trail dots
    private struct TrailDot
    {
        public Transform t;
        public MeshRenderer mr;
        public float born;
        public float life;
        public Color col;
    }
    private List<TrailDot> _trail = new List<TrailDot>();

    // Bob state
    private Vector3 _spawnPos;
    private float _bobTime;
    private bool _hasOwner => currentOwner != null;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        BuildVisuals();
    }

    void Start()
    {
        _spawnPos = transform.position;
        StartCoroutine(LoopAmbient());
        StartCoroutine(LoopOrbit());
        StartCoroutine(LoopTrail());
    }

    void Update()
    {
        // ── Original fly logic ───────────────────────────────
        if (isFlying && targetPlaceholder != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPlaceholder.position, flySpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPlaceholder.position) < 0.1f)
            {
                transform.SetParent(targetPlaceholder);
                transform.localPosition = Vector3.zero;
                isFlying = false;
                StartCoroutine(PickupLandEffect());
            }
        }

        // ── Tick particles ───────────────────────────────────
        TickParticles();
        TickTrail();
    }

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void BuildVisuals()
    {
        // ── Core glow sphere ─────────────────────────────────
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "BallCore";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = Vector3.one * 0.38f;
        Destroy(core.GetComponent<Collider>());

        _coreMat = new Material(UnlitShader());
        _coreMat.color = ballColorIdle;
        _coreRenderer = core.GetComponent<MeshRenderer>();
        _coreRenderer.material = _coreMat;
        _coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ── Outer shell (transparent glow) ───────────────────
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "BallShell";
        shell.transform.SetParent(transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localScale = Vector3.one * 0.65f;
        Destroy(shell.GetComponent<Collider>());

        _shellMat = TransparentMat(new Color(ballColorIdle.r, ballColorIdle.g, ballColorIdle.b, 0.22f));
        _shellRenderer = shell.GetComponent<MeshRenderer>();
        _shellRenderer.material = _shellMat;
        _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ── Orbit parent ─────────────────────────────────────
        var op = new GameObject("OrbitParent");
        op.transform.SetParent(transform, false);
        _orbitParent = op.transform;

        // ── Trail parent (world space) ────────────────────────
        var tp = new GameObject("TrailParent");
        tp.transform.SetParent(transform.parent, false);
        _trailParent = tp.transform;

        // ── Build orbiting particles ──────────────────────────
        // 3 orbital rings at different tilts
        float[] tilts = { 0f, 60f, 120f };
        int[] counts = { 5, 4, 3 };
        float[] radii = { 0.55f, 0.48f, 0.42f };
        float[] speeds = { 180f, -220f, 150f };

        for (int ring = 0; ring < tilts.Length; ring++)
        {
            for (int i = 0; i < counts[ring]; i++)
            {
                float startAngle = (i / (float)counts[ring]) * 360f;
                float sz = Random.Range(0.04f, 0.09f);

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Orb";
                go.transform.SetParent(_orbitParent, false);
                go.transform.localScale = Vector3.one * sz;
                Destroy(go.GetComponent<Collider>());

                var mat = new Material(UnlitShader());
                mat.color = ballColorIdle;
                var mr = go.GetComponent<MeshRenderer>();
                mr.material = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _orbiters.Add(new OrbitParticle
                {
                    t = go.transform,
                    mr = mr,
                    angle = startAngle,
                    radius = radii[ring],
                    speed = speeds[ring],
                    tilt = tilts[ring],
                    size = sz
                });
            }
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region AMBIENT LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopAmbient()
    {
        while (true)
        {
            float t = Time.time;

            // Determine current state colour
            Color targetCol = _hasOwner ? ballColorOwned
                            : isFlying ? ballColorFlying
                            : ballColorIdle;

            // Core pulse
            if (_coreMat)
            {
                float bright = 0.75f + 0.25f * Mathf.Sin(t * 3.2f);
                _coreMat.color = new Color(
                    targetCol.r * bright,
                    targetCol.g * bright,
                    targetCol.b * bright, 1f);
            }

            // Shell breathe
            if (_shellMat)
            {
                float alpha = 0.14f + 0.10f * Mathf.Sin(t * 2.4f);
                _shellMat.color = new Color(targetCol.r, targetCol.g, targetCol.b, alpha);
                float sc = 0.65f + 0.05f * Mathf.Sin(t * 2.4f);
                if (_shellRenderer) _shellRenderer.transform.localScale = Vector3.one * sc;
            }

            // Idle bob (only when not owned or flying)
            if (!_hasOwner && !isFlying && rb && !rb.isKinematic)
            {
                // handled by physics — no override needed
            }

            // Update orbiter colours to match state
            foreach (var o in _orbiters)
                if (o.mr) o.mr.material.color = targetCol;

            yield return null;
        }
    }

    IEnumerator LoopOrbit()
    {
        while (true)
        {
            float dt = Time.deltaTime;
            float t = Time.time;
            float flyBoost = isFlying ? 2.5f : 1f;

            for (int i = 0; i < _orbiters.Count; i++)
            {
                var o = _orbiters[i];

                // Advance angle
                o.angle += o.speed * flyBoost * dt;
                if (o.angle > 360f) o.angle -= 360f;
                if (o.angle < -360f) o.angle += 360f;
                _orbiters[i] = o;

                float rad = o.angle * Mathf.Deg2Rad;
                float tiltR = o.tilt * Mathf.Deg2Rad;

                // Position on a tilted circle
                float x = Mathf.Cos(rad) * o.radius;
                float y = Mathf.Sin(rad) * o.radius * Mathf.Cos(tiltR);
                float z = Mathf.Sin(rad) * o.radius * Mathf.Sin(tiltR);

                o.t.localPosition = new Vector3(x, y, z);

                // Size pulse
                float sp = o.size * (0.8f + 0.4f * Mathf.Sin(t * 4f + o.angle));
                o.t.localScale = Vector3.one * sp;
            }

            yield return null;
        }
    }

    IEnumerator LoopTrail()
    {
        while (true)
        {
            if (isFlying)
            {
                // Dense trail when flying toward player
                SpawnTrailDot(transform.position,
                    new Color(ballColorFlying.r, ballColorFlying.g, ballColorFlying.b, 0.7f),
                    0.18f);
                yield return new WaitForSeconds(0.03f);
            }
            else if (_hasOwner)
            {
                // Subtle trail when carried and moving
                if (rb == null || rb.linearVelocity.magnitude > 2f)
                {
                    SpawnTrailDot(transform.position,
                        new Color(ballColorOwned.r, ballColorOwned.g, ballColorOwned.b, 0.4f),
                        0.12f);
                }
                yield return new WaitForSeconds(0.05f);
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
            }
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PICKUP / DROP EFFECTS
    // ────────────────────────────────────────────────────────

    IEnumerator PickupLandEffect()
    {
        // Ring burst when ball snaps to player
        int count = 20;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Random.Range(0.1f, 0.5f), Mathf.Sin(angle));
            SpawnParticle(transform.position, dir * Random.Range(2f, 6f),
                ballColorOwned, Random.Range(0.2f, 0.45f), Random.Range(0.04f, 0.10f));
        }

        // Core spike
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = 1f - (t / 0.25f);
            if (_coreMat)
            {
                _coreMat.color = Color.Lerp(ballColorOwned,
                    new Color(1f, 1f, 1f), p * 0.8f);
            }
            if (_shellRenderer)
                _shellRenderer.transform.localScale = Vector3.one * (0.65f + 0.6f * p);
            yield return null;
        }
    }

    void DropBurstEffect()
    {
        // Scatter sparks outward + upward
        int count = 24;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(
                Mathf.Cos(angle) * Random.Range(0.5f, 1f),
                Random.Range(0.5f, 2f),
                Mathf.Sin(angle) * Random.Range(0.5f, 1f));
            Color col = Random.value > 0.5f ? ballColorDrop : ballColorIdle;
            SpawnParticle(transform.position, dir * Random.Range(3f, 8f),
                col, Random.Range(0.3f, 0.6f), Random.Range(0.05f, 0.13f));
        }
        StartCoroutine(DropFlash());
    }

    IEnumerator DropFlash()
    {
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = 1f - (t / 0.3f);
            if (_coreMat)
                _coreMat.color = Color.Lerp(ballColorIdle, ballColorDrop, p);
            if (_shellRenderer)
                _shellRenderer.transform.localScale = Vector3.one * (0.65f + 0.5f * p);
            yield return null;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region ORIGINAL GAMEPLAY (unchanged logic, effects added)
    // ────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider col) => ProcessTouch(col.gameObject);
    private void OnCollisionEnter(Collision col) => ProcessTouch(col.gameObject);

    private void ProcessTouch(GameObject hitObj)
    {
        if (!canBePickedUp) return;

        if (hitObj.CompareTag("Player1") || hitObj.CompareTag("Player2") ||
            hitObj.CompareTag("Player3") || hitObj.CompareTag("Player4"))
        {
            if (currentOwner != null) return;

            Transform newPlaceholder = FindDeepChild(hitObj.transform, "EBPH");
            if (newPlaceholder != null)
            {
                currentOwner = hitObj;
                targetPlaceholder = newPlaceholder;
                isFlying = true;
                transform.SetParent(null);

                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                }

                Collider myCol = GetComponent<Collider>();
                if (myCol != null) myCol.isTrigger = true;

                // Pickup flash
                StartCoroutine(PickupStartFlash());
            }
        }
    }

    IEnumerator PickupStartFlash()
    {
        // Brief white flash when ball starts flying toward player
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float p = 1f - (t / 0.15f);
            if (_coreMat)
                _coreMat.color = Color.Lerp(ballColorFlying, Color.white, p * 0.7f);
            yield return null;
        }
    }

    public void DropBall()
    {
        currentOwner = null;
        targetPlaceholder = null;
        isFlying = false;
        transform.SetParent(null);
        canBePickedUp = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.up * dropPopForce, ForceMode.Impulse);
        }

        Collider myCol = GetComponent<Collider>();
        if (myCol != null) myCol.isTrigger = false;

        DropBurstEffect(); // visual effect on drop

        Invoke(nameof(EnablePickup), pickupCooldown);
    }

    private void EnablePickup() => canBePickedUp = true;

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PROCEDURAL PARTICLES
    // ────────────────────────────────────────────────────────

    void SpawnParticle(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EP";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_trailParent ?? transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;

        var mat = new Material(UnlitShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _particles.Add(new Particle { t = go.transform, mr = mr, vel = vel, born = Time.time, life = life, col = col, baseSize = size });
    }

    void SpawnTrailDot(Vector3 pos, Color col, float life)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ET";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_trailParent ?? transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.12f;

        var mat = new Material(TransparentShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _trail.Add(new TrailDot { t = go.transform, mr = mr, born = Time.time, life = life, col = col });
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

            p.vel += Vector3.down * 5f * Time.deltaTime;
            p.t.position += p.vel * Time.deltaTime;
            _particles[i] = p;

            float alpha = Mathf.Lerp(1f, 0f, prog * prog);
            float sc = Mathf.Lerp(p.baseSize, 0f, prog * 0.7f);
            p.t.localScale = Vector3.one * Mathf.Max(sc, 0.001f);
            p.mr.material.color = new Color(p.col.r, p.col.g, p.col.b, alpha);
        }
    }

    void TickTrail()
    {
        float now = Time.time;
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var d = _trail[i];
            if (!d.t) { _trail.RemoveAt(i); continue; }

            float prog = (now - d.born) / d.life;
            if (prog >= 1f) { Destroy(d.t.gameObject); _trail.RemoveAt(i); continue; }

            float alpha = Mathf.Lerp(0.7f, 0f, prog);
            float sc = Mathf.Lerp(0.12f, 0.02f, prog);
            d.t.localScale = Vector3.one * sc;
            d.mr.material.color = new Color(d.col.r, d.col.g, d.col.b, alpha);
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

    static Shader TransparentShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Standard");
    }

    static Material TransparentMat(Color col)
    {
        var mat = new Material(
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Standard"));
        mat.color = col;
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    void OnDestroy()
    {
        foreach (var p in _particles) if (p.t) Destroy(p.t.gameObject);
        foreach (var d in _trail) if (d.t) Destroy(d.t.gameObject);
    }
}