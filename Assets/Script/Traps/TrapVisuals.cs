using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  TrapVisuals — Fixed for large-scale objects (scale 100)
//  All local positions/scales are divided by the object's
//  world scale so effects always appear the right size.
// ============================================================

public class TrapVisuals : MonoBehaviour
{
    [Header("Settings")]
    public Color dangerColor = new Color(1.00f, 0.15f, 0.05f, 1f);
    public Color warningColor = new Color(1.00f, 0.55f, 0.05f, 1f);
    public Color electricColor = new Color(0.80f, 0.95f, 1.00f, 1f);
    public float pulseSpeed = 2.2f;
    public bool isActive = true;

    // ── Built objects ────────────────────────────────────────
    private Light _dangerLight;
    private Transform _lightObj;
    private List<(Transform t, MeshRenderer mr, float angle, float radius, float speed, float tilt)>
        _rings = new List<(Transform, MeshRenderer, float, float, float, float)>();

    private struct Particle
    {
        public Transform t; public MeshRenderer mr;
        public Vector3 vel; public float born; public float life;
        public Color col; public float baseSize;
    }
    private List<Particle> _particles = new List<Particle>();
    private Transform _worldParent;
    private Renderer[] _trapRenderers;
    private bool _triggered;

    // Scale compensator: if object is scale 100, _s=100
    // We divide local values by _s so world-space size stays consistent
    private float _s = 1f;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        _trapRenderers = GetComponentsInChildren<Renderer>();

        // Use the world scale to compensate — works for any scale
        _s = Mathf.Max(transform.lossyScale.x, 0.001f);

        var wp = new GameObject("TrapVFX_" + gameObject.name);
        wp.transform.position = transform.position;
        _worldParent = wp.transform;

        BuildDangerLight();
        BuildWarningRings();
    }

    void Start()
    {
        StartCoroutine(LoopDangerPulse());
        StartCoroutine(LoopSparks());
        StartCoroutine(LoopSmoke());
        StartCoroutine(LoopRings());
    }

    void Update() => TickParticles();

    void OnDestroy()
    {
        if (_worldParent) Destroy(_worldParent.gameObject);
        foreach (var p in _particles) if (p.t) Destroy(p.t.gameObject);
    }

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void BuildDangerLight()
    {
        _lightObj = new GameObject("DangerLight").transform;
        _lightObj.SetParent(transform, false);
        // localPosition is in local space, so divide by scale to get ~0.5 world units up
        _lightObj.localPosition = Vector3.up * (0.5f / _s);

        _dangerLight = _lightObj.gameObject.AddComponent<Light>();
        _dangerLight.type = LightType.Point;
        _dangerLight.color = dangerColor;
        _dangerLight.intensity = 2.5f;
        _dangerLight.range = 5f;   // world-space range, no division needed
        _dangerLight.shadows = LightShadows.None;
    }

    void BuildWarningRings()
    {
        // Ring dots orbit in LOCAL space, so divide radius/height/size by _s
        // so they appear at consistent world-space distances
        var configs = new[]
        {
            (radius: 1.5f, y: 0.3f, speed:  80f, tilt:  0f),
            (radius: 1.2f, y: 0.6f, speed: -60f, tilt: 45f),
            (radius: 0.9f, y: 0.9f, speed: 100f, tilt: 90f),
        };

        foreach (var cfg in configs)
        {
            for (int i = 0; i < 8; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "RingDot";
                go.transform.SetParent(transform, false);
                // Divide by _s: if scale=100, localScale=0.0005 → worldScale=0.05
                go.transform.localScale = Vector3.one * (0.05f / _s);
                Destroy(go.GetComponent<Collider>());

                var mat = new Material(UnlitShader());
                mat.color = i % 2 == 0 ? dangerColor : warningColor;
                var mr = go.GetComponent<MeshRenderer>();
                mr.material = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                float startAngle = (i / 8f) * 360f;
                // Store radius/y divided by _s for local-space positioning
                _rings.Add((go.transform, mr, startAngle, cfg.radius / _s, cfg.speed, cfg.tilt));
            }
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region AMBIENT LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopDangerPulse()
    {
        while (true)
        {
            float t = Time.time;
            if (!isActive)
            {
                if (_dangerLight) _dangerLight.intensity = 0f;
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * pulseSpeed);

            if (_dangerLight)
            {
                _dangerLight.intensity = Mathf.Lerp(1.2f, 3.5f, pulse);
                _dangerLight.color = Color.Lerp(warningColor, dangerColor, pulse);
                _dangerLight.range = Mathf.Lerp(4f, 7f, pulse);
            }

            foreach (var r in _trapRenderers)
            {
                if (!r) continue;
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        Color base_ = Color.Lerp(
                            new Color(0.15f, 0.02f, 0.02f),
                            new Color(0.55f, 0.08f, 0.02f),
                            pulse * 0.6f);
                        mat.color = base_;
                    }
                }
            }
            yield return null;
        }
    }

    IEnumerator LoopRings()
    {
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _rings.Count; i++)
            {
                var (tr, mr, angle, radius, speed, tilt) = _rings[i];
                float newAngle = angle + speed * Time.deltaTime;
                if (newAngle > 360f) newAngle -= 360f;
                if (newAngle < -360f) newAngle += 360f;
                _rings[i] = (tr, mr, newAngle, radius, speed, tilt);

                float rad = newAngle * Mathf.Deg2Rad;
                float tiltR = tilt * Mathf.Deg2Rad;
                int ringIdx = i / 8;
                float yOff = ringIdx == 0 ? 0.3f / _s : ringIdx == 1 ? 0.6f / _s : 0.9f / _s;

                float x = Mathf.Cos(rad) * radius;
                float y = yOff + Mathf.Sin(rad) * radius * Mathf.Sin(tiltR);
                float z = Mathf.Sin(rad) * radius * Mathf.Cos(tiltR);
                tr.localPosition = new Vector3(x, y, z);

                float dotIdx = i % 8;
                float tw = (0.04f + 0.03f * Mathf.Sin(t * 5f + dotIdx)) / _s;
                tr.localScale = Vector3.one * tw;

                Color c = dotIdx % 2 == 0 ? dangerColor : warningColor;
                c.a = isActive ? (0.6f + 0.4f * Mathf.Sin(t * 3f + dotIdx)) : 0.1f;
                mr.material.color = c;
            }
            yield return null;
        }
    }

    IEnumerator LoopSparks()
    {
        while (true)
        {
            if (!isActive) { yield return new WaitForSeconds(0.5f); continue; }

            int count = Random.Range(1, 3);
            for (int i = 0; i < count; i++)
            {
                // World-space position near the trap
                Vector3 pos = transform.position + new Vector3(
                    Random.Range(-0.4f, 0.4f),
                    Random.Range(0.1f, 0.5f),
                    Random.Range(-0.4f, 0.4f));
                Vector3 vel = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(0.3f, 0.8f),
                    Random.Range(-0.5f, 0.5f));
                Color col = Random.value > 0.6f ? electricColor : dangerColor;
                col.a = 1f;
                // World-space size — no scale division needed
                SpawnParticle(pos, vel, col, Random.Range(0.15f, 0.3f), Random.Range(0.04f, 0.08f));
            }
            yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));
        }
    }

    IEnumerator LoopSmoke()
    {
        while (true)
        {
            if (!isActive) { yield return new WaitForSeconds(0.5f); continue; }

            Vector2 rand = Random.insideUnitCircle * 0.3f;
            Vector3 pos = transform.position + new Vector3(rand.x, 0.2f, rand.y);
            Vector3 vel = new Vector3(
                Random.Range(-0.1f, 0.1f),
                Random.Range(0.3f, 0.6f),
                Random.Range(-0.1f, 0.1f));

            Color smokeCol = new Color(0.22f, 0.04f, 0.04f, 0.4f);
            SpawnParticle(pos, vel, smokeCol, Random.Range(0.6f, 1.0f), Random.Range(0.06f, 0.10f));

            yield return new WaitForSeconds(Random.Range(0.25f, 0.55f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PUBLIC HOOKS
    // ────────────────────────────────────────────────────────

    public void OnTrapTriggered()
    {
        if (_triggered) return;
        StartCoroutine(TriggerEffect());
    }

    public void SetActive(bool active)
    {
        isActive = active;
        if (_dangerLight) _dangerLight.intensity = active ? 2.5f : 0.2f;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region TRIGGER EFFECT
    // ────────────────────────────────────────────────────────

    IEnumerator TriggerEffect()
    {
        _triggered = true;

        // World-space burst — sizes and speeds are already world-space
        for (int i = 0; i < 36; i++)
        {
            float angle = (i / 36f) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Random.Range(0.3f, 1.0f), Mathf.Sin(angle));
            Color col = i % 3 == 0 ? electricColor : i % 3 == 1 ? dangerColor : warningColor;
            col.a = 1f;
            SpawnParticle(
                transform.position + Vector3.up * 0.3f,
                dir * Random.Range(1.5f, 3.5f),
                col, Random.Range(0.3f, 0.6f), Random.Range(0.05f, 0.10f));
        }

        // Light spike
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float p = 1f - t / 0.15f;
            if (_dangerLight)
            {
                _dangerLight.intensity = Mathf.Lerp(2.5f, 16f, p);
                _dangerLight.range = Mathf.Lerp(5f, 12f, p);
                _dangerLight.color = Color.Lerp(dangerColor, Color.white, p * 0.5f);
            }
            yield return null;
        }

        // Warning flash
        for (int flash = 0; flash < 6; flash++)
        {
            if (_dangerLight) _dangerLight.intensity = 0.3f;
            yield return new WaitForSeconds(0.1f);
            if (_dangerLight) _dangerLight.intensity = 3f;
            yield return new WaitForSeconds(0.1f);
        }

        _triggered = false;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PARTICLES
    // ────────────────────────────────────────────────────────

    void SpawnParticle(Vector3 worldPos, Vector3 vel, Color col, float life, float worldSize)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TP";
        Destroy(go.GetComponent<Collider>());
        // Parent to world parent (not the scaled trap!) so scale doesn't affect size
        go.transform.SetParent(_worldParent, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * worldSize; // true world-space size
        go.transform.SetParent(null, true); // detach so parent scale doesn't multiply

        var mat = new Material(UnlitShader());
        mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _particles.Add(new Particle
        { t = go.transform, mr = mr, vel = vel, born = Time.time, life = life, col = col, baseSize = worldSize });
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

            p.vel += Vector3.up * 0.4f * Time.deltaTime;
            p.vel *= (1f - 1.5f * Time.deltaTime);
            p.t.position += p.vel * Time.deltaTime;
            _particles[i] = p;

            float sc = p.baseSize < 0.09f
                ? p.baseSize * Mathf.Lerp(1f, 0f, prog)
                : p.baseSize * (1f + 0.4f * Mathf.Sin(prog * Mathf.PI));
            p.t.localScale = Vector3.one * Mathf.Max(sc, 0.001f);

            float alpha = Mathf.Lerp(p.col.a, 0f, prog * prog);
            p.mr.material.color = new Color(p.col.r, p.col.g, p.col.b, alpha);
        }
    }

    #endregion

    static Shader UnlitShader() =>
        Shader.Find("Universal Render Pipeline/Unlit")
        ?? Shader.Find("Unlit/Color")
        ?? Shader.Find("Standard");
}