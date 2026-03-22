using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ArenaVisuals — Pure Lighting Edition (No geometry rings)
//
//  SETUP — only 2 things needed:
//  1. Set Panel Parent → drag "High Arena" from Hierarchy
//  2. Hit Play
//
//  Everything else is automatic. No radius to tune.
//  Uses actual scene bounds to size itself correctly.
//
//  EFFECTS:
//  - Soft ambient fill from above (deep blue)
//  - Centre pulse light on the floor
//  - 6 perimeter point lights low to ground
//  - 3 slow roaming fill lights
//  - 4 spawn colour glows (auto-positioned if no spawns assigned)
//  - Tiny edge sparks (sized relative to scene)
//  - Slow falling micro embers
//  - Panel auto-detection + gentle colour shift per panel
//  - All effects auto-size from scene bounds — no manual tuning
// ============================================================

public class ArenaVisuals : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Drag 'High Arena' from Hierarchy")]
    public Transform panelParent;

    [Header("Spawn Lights (optional)")]
    public Transform spawn1, spawn2, spawn3, spawn4;

    [Header("Intensity")]
    [Range(0.2f, 3f)] public float lightMult = 1f;

    // ── Colours ──────────────────────────────────────────────
    static readonly Color ColCyan = new Color(0.00f, 0.88f, 1.00f, 1f);
    static readonly Color ColOrange = new Color(1.00f, 0.42f, 0.06f, 1f);
    static readonly Color ColViolet = new Color(0.65f, 0.12f, 1.00f, 1f);
    static readonly Color ColLime = new Color(0.20f, 1.00f, 0.30f, 1f);
    static readonly Color ColIce = new Color(0.75f, 0.92f, 1.00f, 1f);
    static readonly Color ColDeep = new Color(0.05f, 0.18f, 0.55f, 1f);
    static readonly Color ColTeal = new Color(0.00f, 0.65f, 0.70f, 1f);

    static readonly Color[] PanelColors = {
        new Color(0.00f, 0.88f, 1.00f, 1f),
        new Color(0.55f, 0.10f, 1.00f, 1f),
        new Color(0.00f, 0.65f, 0.70f, 1f),
        new Color(0.05f, 0.40f, 1.00f, 1f),
        new Color(0.20f, 1.00f, 0.80f, 1f),
    };

    // ── Derived from scene ───────────────────────────────────
    private Vector3 _centre;
    private float _r;   // arena radius — auto-detected from panelParent bounds
    private float _u;   // spark size unit

    // ── Lights ───────────────────────────────────────────────
    private Light _centreLight;
    private Light _ambientFill;
    private List<Light> _perimLights = new List<Light>();
    private List<Light> _spawnLights = new List<Light>();

    private struct Roamer
    {
        public Transform t; public Light l;
        public Vector3 vel; public float phase; public Color colA, colB;
    }
    private List<Roamer> _roamers = new List<Roamer>();

    // ── Panels ───────────────────────────────────────────────
    private struct Panel
    {
        public MeshRenderer mr; public Material mat;
        public Light light; public float phase;
        public Color colA, colB; public float lightRange;
    }
    private List<Panel> _panels = new List<Panel>();

    // ── Sparks ───────────────────────────────────────────────
    private struct Spark
    {
        public Transform t; public Material mat;
        public Vector3 vel; public float born; public float life;
        public Color col; public float sz;
    }
    private List<Spark> _sparks = new List<Spark>();

    // ────────────────────────────────────────────────────────
    void Start()
    {
        // Auto-detect arena bounds from panelParent
        if (panelParent != null)
        {
            Bounds b = GetChildBounds(panelParent);
            _centre = b.center;
            _r = Mathf.Max(b.extents.x, b.extents.z);
        }
        else
        {
            _centre = Vector3.zero;
            _r = 20f;
        }

        // Spark size = 0.3% of radius — always proportional, never giant
        _u = _r * 0.003f;

        Debug.Log($"[ArenaVisuals] Centre={_centre} Radius={_r:F1} SparkUnit={_u:F3}");

        BuildAmbientFill();
        BuildCentreLight();
        BuildPerimeterLights();
        BuildRoamers();
        BuildSpawnLights();
        DetectPanels();

        StartCoroutine(LoopAmbient());
        StartCoroutine(LoopCentre());
        StartCoroutine(LoopPerimeter());
        StartCoroutine(LoopRoamers());
        StartCoroutine(LoopSpawnLights());
        StartCoroutine(LoopPanels());
        StartCoroutine(LoopEdgeSparks());
        StartCoroutine(LoopPanelSparks());
    }

    void Update() => TickSparks();

    // ────────────────────────────────────────────────────────
    #region BOUNDS DETECTION
    // ────────────────────────────────────────────────────────

    Bounds GetChildBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 20f);

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void BuildAmbientFill()
    {
        // Soft deep-blue fill from high above — subtle base tone
        var go = MakeLight("Ambient",
            _centre + Vector3.up * _r * 0.5f,
            new Color(0.08f, 0.15f, 0.40f),
            0.6f * lightMult, _r * 2.5f);
        _ambientFill = go;
    }

    void BuildCentreLight()
    {
        // Single teal point at floor centre
        var go = MakeLight("Centre",
            _centre + Vector3.up * _r * 0.02f,
            ColTeal, 1.5f * lightMult, _r * 0.7f);
        _centreLight = go;
    }

    void BuildPerimeterLights()
    {
        // 6 low point lights around the edge — no geometry, just light
        Color[] cols = { ColCyan, ColDeep, ColViolet, ColTeal, ColCyan, ColDeep };
        for (int i = 0; i < 6; i++)
        {
            float a = (i / 6f) * Mathf.PI * 2f;
            Vector3 p = _centre + new Vector3(
                Mathf.Cos(a) * _r * 0.75f,
                _r * 0.01f,
                Mathf.Sin(a) * _r * 0.75f);
            var l = MakeLight("Peri_" + i, p, cols[i],
                1.2f * lightMult, _r * 0.6f);
            _perimLights.Add(l);
        }
    }

    void BuildRoamers()
    {
        var pairs = new[] {
            (ColCyan,   ColTeal),
            (ColViolet, ColDeep),
            (ColIce,    ColCyan),
        };
        for (int i = 0; i < 3; i++)
        {
            float a = (i / 3f) * Mathf.PI * 2f;
            Vector3 p = _centre + new Vector3(
                Mathf.Cos(a) * _r * 0.3f, _r * 0.01f,
                Mathf.Sin(a) * _r * 0.3f);
            var l = MakeLight("Roam_" + i, p, pairs[i].Item1,
                0.9f * lightMult, _r * 0.55f);
            float spd = _r * Random.Range(0.03f, 0.07f);
            var vel = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * spd;
            _roamers.Add(new Roamer
            {
                t = l.transform,
                l = l,
                vel = vel,
                phase = i * Mathf.PI * 0.66f,
                colA = pairs[i].Item1,
                colB = pairs[i].Item2
            });
        }
    }

    void BuildSpawnLights()
    {
        Transform[] spawns = { spawn1, spawn2, spawn3, spawn4 };
        Color[] cols = { ColCyan, ColOrange, ColViolet, ColLime };
        for (int i = 0; i < 4; i++)
        {
            Vector3 pos;
            if (spawns[i] != null)
                pos = spawns[i].position + Vector3.up * _r * 0.01f;
            else
            {
                float a = (i / 4f) * Mathf.PI * 2f;
                pos = _centre + new Vector3(
                    Mathf.Cos(a) * _r * 0.45f, _r * 0.01f,
                    Mathf.Sin(a) * _r * 0.45f);
            }
            var l = MakeLight("Spawn_" + i, pos, cols[i],
                0.8f * lightMult, _r * 0.35f);
            _spawnLights.Add(l);
        }
    }

    void DetectPanels()
    {
        if (panelParent == null) return;

        var all = panelParent.GetComponentsInChildren<MeshRenderer>(true);
        float innerD = _r * 0.5f;
        float outerD = _r * 1.5f;

        var skip = new HashSet<string> {
            "S","PeriDot","SpawnDisc","CentreOrb","FloorRing",
            "PanelDot","Ambient","Centre","Roam","Peri","Spawn",
            "Edge","Corner","Halo","AuraRing","GlowSphere",
            "DizzyStar","Trail","Orb","BallCore","BallShell",
            // LED Lamp objects — never touch these
            "LEDLamp","Sticker","Neon","PivotA","PivotB",
            "BeamA","BeamB","BoxLight","NL","SlicePivot",
            "Cubie","RubikRoot","FaceGlow","Trace","CoreLight"
        };

        var found = new List<(MeshRenderer mr, float angle)>();
        foreach (var mr in all)
        {
            if (!mr) continue;
            string n = mr.gameObject.name;
            if (skip.Contains(n) || n.Length <= 2) continue;

            Vector3 c = mr.bounds.center;
            float flatX = c.x - _centre.x;
            float flatZ = c.z - _centre.z;
            float dist = Mathf.Sqrt(flatX * flatX + flatZ * flatZ);

            if (dist >= innerD && dist <= outerD
                && c.y > _centre.y - _r * 0.3f
                && mr.bounds.size.magnitude > _r * 0.08f) // must be large enough to be a panel
                found.Add((mr, Mathf.Atan2(flatZ, flatX)));
        }

        found.Sort((a, b) => a.angle.CompareTo(b.angle));
        Debug.Log($"[ArenaVisuals] {found.Count} panels detected.");

        for (int i = 0; i < found.Count; i++)
        {
            var (mr, _) = found[i];
            int ci = i % PanelColors.Length;
            Color colA = PanelColors[ci];
            Color colB = PanelColors[(ci + 1) % PanelColors.Length];

            // Very dark tint on the panel surface
            var mat = new Material(UnlitShader());
            mat.color = new Color(colA.r * 0.05f, colA.g * 0.06f, colA.b * 0.08f, 1f);
            mr.material = mat;

            // Small point light per panel — range clamped to reasonable size
            float range = Mathf.Clamp(mr.bounds.size.magnitude * 0.4f, _r * 0.05f, _r * 0.3f);
            var l = MakeLight("PL_" + i,
                mr.bounds.center + Vector3.up * _r * 0.01f,
                colA, lightMult * 1.0f, range);

            _panels.Add(new Panel
            {
                mr = mr,
                mat = mat,
                light = l,
                phase = (i / (float)found.Count) * Mathf.PI * 2f,
                colA = colA,
                colB = colB,
                lightRange = range
            });
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopAmbient()
    {
        while (true)
        {
            if (_ambientFill)
                _ambientFill.intensity = (0.5f + 0.15f * Mathf.Sin(Time.time * 0.4f)) * lightMult;
            yield return null;
        }
    }

    IEnumerator LoopCentre()
    {
        Color[] cycle = { ColTeal, ColCyan, ColViolet, ColIce, ColTeal };
        int idx = 0;
        while (true)
        {
            Color from = cycle[idx % cycle.Length];
            Color to = cycle[(idx + 1) % cycle.Length];
            float t = 0f;
            while (t < 5f)
            {
                t += Time.deltaTime;
                if (_centreLight)
                {
                    _centreLight.color = Color.Lerp(from, to, t / 5f);
                    _centreLight.intensity = (1.2f + 0.4f * Mathf.Sin(Time.time * 1.5f)) * lightMult;
                }
                yield return null;
            }
            idx++;
        }
    }

    IEnumerator LoopPerimeter()
    {
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _perimLights.Count; i++)
            {
                var l = _perimLights[i]; if (!l) continue;
                float wave = 0.6f + 0.4f * Mathf.Sin(t * 1.2f + i * (Mathf.PI * 2f / 6f));
                l.intensity = 1.2f * lightMult * wave;
                l.range = _r * (0.5f + 0.12f * wave);
            }
            yield return null;
        }
    }

    IEnumerator LoopRoamers()
    {
        while (true)
        {
            for (int i = 0; i < _roamers.Count; i++)
            {
                var r = _roamers[i]; if (!r.t) continue;
                r.t.position += r.vel * Time.deltaTime;

                Vector3 flat = new Vector3(
                    r.t.position.x - _centre.x, 0f,
                    r.t.position.z - _centre.z);
                if (flat.magnitude > _r * 0.6f)
                {
                    r.vel = -flat.normalized * r.vel.magnitude;
                    r.vel += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * _r * 0.01f;
                }
                r.t.position = new Vector3(r.t.position.x,
                    _centre.y + _r * 0.01f + Mathf.Sin(Time.time * 1.1f + r.phase) * _r * 0.005f,
                    r.t.position.z);

                float cp = (Mathf.Sin(Time.time * 0.6f + r.phase) + 1f) * 0.5f;
                r.l.color = Color.Lerp(r.colA, r.colB, cp);
                r.l.intensity = (0.7f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 1.4f + r.phase))) * lightMult;
                _roamers[i] = r;
            }
            yield return null;
        }
    }

    IEnumerator LoopSpawnLights()
    {
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _spawnLights.Count; i++)
            {
                var l = _spawnLights[i]; if (!l) continue;
                l.intensity = (0.6f + 0.3f * Mathf.Sin(t * 2f + i * Mathf.PI * 0.5f)) * lightMult;
            }
            yield return null;
        }
    }

    IEnumerator LoopPanels()
    {
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i]; if (!p.light || !p.mat) continue;
                float wave = 0.4f + 0.6f * Mathf.Sin(t * 1.8f + p.phase);
                float cp = (Mathf.Sin(t * 0.35f + p.phase) + 1f) * 0.5f;
                Color cur = Color.Lerp(p.colA, p.colB, cp);
                p.light.color = cur;
                p.light.intensity = lightMult * wave;
                p.light.range = p.lightRange * (0.85f + 0.2f * wave);
                p.mat.color = new Color(
                    cur.r * (0.03f + 0.09f * wave),
                    cur.g * (0.03f + 0.09f * wave),
                    cur.b * (0.05f + 0.09f * wave), 1f);
            }
            yield return null;
        }
    }

    // ── SPARKS ───────────────────────────────────────────────

    IEnumerator LoopEdgeSparks()
    {
        while (true)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 p = _centre + new Vector3(
                Mathf.Cos(a) * _r * 0.82f,
                _r * Random.Range(0.005f, 0.04f),
                Mathf.Sin(a) * _r * 0.82f);
            Vector3 inward = (_centre - p).normalized;
            Vector3 vel = inward * _r * Random.Range(0.02f, 0.06f)
                           + Vector3.up * _r * Random.Range(0.01f, 0.04f);
            Color col = Random.value > 0.5f ? ColCyan : ColTeal;
            col.a = Random.Range(0.6f, 1f);
            Emit(p, vel, col, Random.Range(0.5f, 1.2f), _u * Random.Range(1f, 2.5f));
            yield return new WaitForSeconds(Random.Range(0.06f, 0.2f));
        }
    }

    IEnumerator LoopEmbers()
    {
        while (true)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(0f, _r * 0.7f);
            Vector3 p = _centre + new Vector3(Mathf.Cos(a) * r,
                _r * Random.Range(0.15f, 0.45f), Mathf.Sin(a) * r);
            Vector3 vel = new Vector3(
                Random.Range(-1f, 1f) * _r * 0.002f,
                -_r * Random.Range(0.003f, 0.008f),
                Random.Range(-1f, 1f) * _r * 0.002f);
            Color[] cols = { ColCyan, ColIce, ColViolet, Color.white };
            Color col = cols[Random.Range(0, cols.Length)];
            col.a = Random.Range(0.2f, 0.5f);
            Emit(p, vel, col, Random.Range(3f, 8f), _u * Random.Range(1f, 2f));
            yield return new WaitForSeconds(Random.Range(0.15f, 0.4f));
        }
    }

    IEnumerator LoopPanelSparks()
    {
        while (true)
        {
            if (_panels.Count > 0)
            {
                var p = _panels[Random.Range(0, _panels.Count)];
                if (p.mr != null)
                {
                    Vector3 origin = p.mr.bounds.center;
                    Vector3 inward = (_centre - origin).normalized;
                    for (int i = 0; i < Random.Range(1, 4); i++)
                    {
                        Vector3 vel = inward * _r * Random.Range(0.02f, 0.05f)
                            + Vector3.up * _r * Random.Range(0.01f, 0.04f)
                            + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * _r * 0.008f;
                        Color col = p.colA; col.a = Random.Range(0.6f, 1f);
                        Emit(origin + Vector3.up * _r * Random.Range(0.005f, 0.02f),
                            vel, col, Random.Range(0.3f, 0.8f), _u * Random.Range(1f, 2.5f));
                    }
                }
            }
            yield return new WaitForSeconds(Random.Range(0.08f, 0.3f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SPARK ENGINE
    // ────────────────────────────────────────────────────────

    void Emit(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        size = Mathf.Max(size, 0.01f); // never zero
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "S";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(null);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var mat = new Material(UnlitShader()); mat.color = col;
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _sparks.Add(new Spark
        {
            t = go.transform,
            mat = mat,
            vel = vel,
            born = Time.time,
            life = life,
            col = col,
            sz = size
        });
    }

    void TickSparks()
    {
        float now = Time.time;
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            if (!s.t) { _sparks.RemoveAt(i); continue; }
            float prog = (now - s.born) / s.life;
            if (prog >= 1f) { Destroy(s.t.gameObject); _sparks.RemoveAt(i); continue; }
            s.vel *= (1f - 0.85f * Time.deltaTime);
            s.vel.y -= _r * 0.001f * Time.deltaTime;
            s.t.position += s.vel * Time.deltaTime;
            _sparks[i] = s;
            s.t.localScale = Vector3.one * s.sz;
            s.mat.color = new Color(s.col.r, s.col.g, s.col.b,
                Mathf.Max(s.col.a * Mathf.Pow(1f - prog, 1.4f), 0f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region HELPERS
    // ────────────────────────────────────────────────────────

    Light MakeLight(string name, Vector3 pos, Color col, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = col;
        l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static Shader UnlitShader() =>
        Shader.Find("Universal Render Pipeline/Unlit")
        ?? Shader.Find("Unlit/Color")
        ?? Shader.Find("Standard");

    void OnDrawGizmosSelected()
    {
        if (panelParent == null) return;
        Bounds b = GetChildBounds(panelParent);
        float r = Mathf.Max(b.extents.x, b.extents.z);
        Gizmos.color = new Color(0f, 0.88f, 1f, 0.25f);
        Gizmos.DrawWireSphere(b.center, r);
    }

    #endregion
}