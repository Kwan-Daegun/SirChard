using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  BoxVisuals — Energy Crack Edition
//
//  Add to the "Box" PARENT. Auto-finds all Cube children.
//
//  EFFECTS:
//  - Dark box body stays dark (no solid colours)
//  - Thin cyan edge outlines breathing slowly
//  - Moving energy cracks — jagged lines on the faces that
//    pulse and crawl like contained energy trying to escape
//  - Soft inner glow light (low, no shadows)
//  - Very occasional spark drifting upward
//  - Hit reaction — cracks flare bright + sparks burst out
// ============================================================

public class BoxVisuals : MonoBehaviour
{
    [Header("Settings")]
    [Range(0.1f, 2f)] public float intensity = 0.8f;
    [Range(0.005f, 0.05f)] public float edgeWidth = 0.022f;

    static readonly Color[] Palette = {
        new Color(0.00f, 0.88f, 1.00f, 1f),
        new Color(0.00f, 0.65f, 0.85f, 1f),
        new Color(0.20f, 0.75f, 1.00f, 1f),
        new Color(0.55f, 0.10f, 1.00f, 1f),
        new Color(0.00f, 0.55f, 0.75f, 1f),
    };

    // ── Per-cube data ─────────────────────────────────────────
    private class CubeData
    {
        public Transform root;
        public Renderer rend;
        public Material bodyMat;
        public Light light;
        public List<Material> edgeMats = new List<Material>();
        public List<CrackData> cracks = new List<CrackData>();
        public Color col;
        public float phase;
        public bool wasHit;
    }

    // A "crack" is a chain of thin cube segments forming a jagged line
    private class CrackData
    {
        public List<Transform> segs = new List<Transform>();
        public List<Material> mats = new List<Material>();
        public float phase;
        public float speed;    // crawl speed
        public float offset;   // position along face
        public bool vertical; // crack direction
    }

    private List<CubeData> _cubes = new List<CubeData>();

    // ── Spark pool ───────────────────────────────────────────
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
        FindAndBuild();
        StartCoroutine(LoopEdges());
        StartCoroutine(LoopCracks());
        StartCoroutine(LoopSparks());
    }

    void Update() => TickSparks();

    void OnDestroy()
    {
        foreach (var s in _sparks) if (s.t) Destroy(s.t.gameObject);
    }

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void FindAndBuild()
    {
        var skip = new HashSet<string> {
            "Edge","Crack","CrackSeg","BoxLight","S","ScanLine",
            "Halo","BeamA","BeamB","PivotA","PivotB","PanelDot",
            "CentreOrb","FloorRing","TopBeam","FaceGlow","Trace","CoreLight"
        };

        int idx = 0;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr) continue;
            string n = mr.gameObject.name;
            if (skip.Contains(n) || n.Length <= 2) continue;
            if (!mr.GetComponent<Collider>()) continue;
            BuildCube(mr, idx++);
        }
        Debug.Log($"[BoxVisuals] {_cubes.Count} cubes built.");
    }

    void BuildCube(MeshRenderer mr, int idx)
    {
        Color col = Palette[idx % Palette.Length];
        Bounds b = mr.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;
        float w = edgeWidth;

        var cube = new CubeData
        {
            root = mr.transform,
            rend = mr,
            col = col,
            phase = (idx / (float)Palette.Length) * Mathf.PI * 2f
        };

        // ── Dark body ────────────────────────────────────────
        cube.bodyMat = new Material(Unlit());
        cube.bodyMat.color = new Color(col.r * 0.04f, col.g * 0.05f, col.b * 0.07f, 1f);
        mr.material = cube.bodyMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // ── Edge outlines ────────────────────────────────────
        var edgeDefs = new (Vector3 p, Vector3 sc)[]
        {
            (c+new Vector3(0, e.y, e.z),  new Vector3(e.x*2,w,w)),
            (c+new Vector3(0,-e.y, e.z),  new Vector3(e.x*2,w,w)),
            (c+new Vector3(0, e.y,-e.z),  new Vector3(e.x*2,w,w)),
            (c+new Vector3(0,-e.y,-e.z),  new Vector3(e.x*2,w,w)),
            (c+new Vector3( e.x,0, e.z),  new Vector3(w,e.y*2,w)),
            (c+new Vector3(-e.x,0, e.z),  new Vector3(w,e.y*2,w)),
            (c+new Vector3( e.x,0,-e.z),  new Vector3(w,e.y*2,w)),
            (c+new Vector3(-e.x,0,-e.z),  new Vector3(w,e.y*2,w)),
            (c+new Vector3( e.x, e.y,0),  new Vector3(w,w,e.z*2)),
            (c+new Vector3(-e.x, e.y,0),  new Vector3(w,w,e.z*2)),
            (c+new Vector3( e.x,-e.y,0),  new Vector3(w,w,e.z*2)),
            (c+new Vector3(-e.x,-e.y,0),  new Vector3(w,w,e.z*2)),
        };
        foreach (var (pos, sc) in edgeDefs)
        {
            var go = MakeWS("Edge", PrimitiveType.Cube, pos, sc, mr.transform);
            var mat = new Material(Unlit());
            Color ec = col; ec.a = 0.7f; mat.color = ec;
            go.GetComponent<MeshRenderer>().material = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            cube.edgeMats.Add(mat);
        }

        // ── Energy cracks (2–3 per cube) ─────────────────────
        int crackCount = Random.Range(2, 4);
        for (int ci = 0; ci < crackCount; ci++)
            BuildCrack(cube, c, e, col, ci, crackCount);

        // ── Point light ───────────────────────────────────────
        var lgo = new GameObject("BoxLight");
        lgo.transform.position = c + Vector3.up * (e.y + 0.2f);
        lgo.transform.SetParent(mr.transform, true);
        cube.light = lgo.AddComponent<Light>();
        cube.light.type = LightType.Point;
        cube.light.color = col;
        cube.light.intensity = intensity * 1.0f;
        cube.light.range = Mathf.Max(e.x, e.y, e.z) * 3f;
        cube.light.shadows = LightShadows.None;

        // ── Hit reactor ───────────────────────────────────────
        var reactor = mr.gameObject.GetComponent<BoxHitReactor>();
        if (!reactor) reactor = mr.gameObject.AddComponent<BoxHitReactor>();
        reactor.Setup(this, _cubes.Count);

        _cubes.Add(cube);
    }

    void BuildCrack(CubeData cube, Vector3 c, Vector3 e,
        Color col, int crackIdx, int total)
    {
        // Each crack lives on one face of the box
        // We pick a face, then build a jagged chain of thin segments
        // Segments are VERY thin so they look like crack lines

        int face = crackIdx % 6; // which face: 0=front,1=back,2=right,3=left,4=top,5=bottom
        float segW = edgeWidth * 0.35f;  // crack width — thinner than edges
        int segCount = Random.Range(4, 8);
        float faceOff = 0.005f;  // sits just proud of the face

        var crack = new CrackData
        {
            phase = crackIdx * Mathf.PI * 0.7f + cube.phase,
            speed = Random.Range(0.3f, 0.8f),
            offset = Random.Range(-0.4f, 0.4f),
            vertical = Random.value > 0.5f
        };

        // Build jagged segments along the face
        Vector3 faceNormal;
        Vector3 faceRight;
        Vector3 faceUp;
        Vector3 faceCenter;
        float faceHalfW;
        float faceHalfH;

        switch (face)
        {
            case 0: faceNormal = Vector3.forward; faceCenter = c + new Vector3(0, 0, e.z + faceOff); faceRight = Vector3.right; faceUp = Vector3.up; faceHalfW = e.x; faceHalfH = e.y; break;
            case 1: faceNormal = Vector3.back; faceCenter = c + new Vector3(0, 0, -e.z - faceOff); faceRight = Vector3.right; faceUp = Vector3.up; faceHalfW = e.x; faceHalfH = e.y; break;
            case 2: faceNormal = Vector3.right; faceCenter = c + new Vector3(e.x + faceOff, 0, 0); faceRight = Vector3.forward; faceUp = Vector3.up; faceHalfW = e.z; faceHalfH = e.y; break;
            case 3: faceNormal = Vector3.left; faceCenter = c + new Vector3(-e.x - faceOff, 0, 0); faceRight = Vector3.forward; faceUp = Vector3.up; faceHalfW = e.z; faceHalfH = e.y; break;
            case 4: faceNormal = Vector3.up; faceCenter = c + new Vector3(0, e.y + faceOff, 0); faceRight = Vector3.right; faceUp = Vector3.forward; faceHalfW = e.x; faceHalfH = e.z; break;
            default: faceNormal = Vector3.down; faceCenter = c + new Vector3(0, -e.y - faceOff, 0); faceRight = Vector3.right; faceUp = Vector3.forward; faceHalfW = e.x; faceHalfH = e.z; break;
        }

        // Starting position on the face
        float startAlong = crack.offset * (crack.vertical ? faceHalfH : faceHalfW);
        float crossPos = crack.offset * (crack.vertical ? faceHalfW : faceHalfH) * 0.3f;

        Vector3 along = crack.vertical ? faceUp : faceRight;
        Vector3 cross = crack.vertical ? faceRight : faceUp;

        float segLen = (crack.vertical ? faceHalfH : faceHalfW) * 2f / segCount;

        for (int si = 0; si < segCount; si++)
        {
            float t = si / (float)(segCount - 1);
            float jitter = (si == 0 || si == segCount - 1) ? 0f
                        : Random.Range(-0.25f, 0.25f) * (crack.vertical ? faceHalfW : faceHalfH);

            Vector3 pos = faceCenter
                + along * (startAlong + (t - 0.5f) * (crack.vertical ? faceHalfH : faceHalfW) * 2f)
                + cross * (crossPos + jitter)
                + faceNormal * 0.002f;

            // Segment scale — thin strip
            Vector3 sc = crack.vertical
                ? new Vector3(segW, segLen * 0.55f, segW * 0.5f)
                : new Vector3(segLen * 0.55f, segW, segW * 0.5f);

            var go = MakeWS("CrackSeg", PrimitiveType.Cube, pos, sc, cube.root);
            // Slight random rotation to make it look jagged
            go.transform.Rotate(faceNormal, Random.Range(-25f, 25f), Space.World);

            var mat = new Material(Unlit());
            Color cc = col; cc.a = 0f; // start invisible
            mat.color = cc;
            go.GetComponent<MeshRenderer>().material = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            crack.segs.Add(go.transform);
            crack.mats.Add(mat);
        }

        cube.cracks.Add(crack);
    }

    GameObject MakeWS(string name, PrimitiveType type,
        Vector3 worldPos, Vector3 worldScale, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = worldPos;
        go.transform.localScale = worldScale;
        if (parent) go.transform.SetParent(parent, true);
        return go;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopEdges()
    {
        while (true)
        {
            float t = Time.time;
            foreach (var cube in _cubes)
            {
                if (cube.wasHit) continue;
                float breathe = 0.55f + 0.45f * Mathf.Sin(t * 0.9f + cube.phase);
                foreach (var m in cube.edgeMats)
                    if (m) m.color = new Color(cube.col.r, cube.col.g, cube.col.b,
                        breathe * 0.75f);
                if (cube.bodyMat)
                    cube.bodyMat.color = new Color(
                        cube.col.r * (0.03f + 0.04f * breathe),
                        cube.col.g * (0.03f + 0.04f * breathe),
                        cube.col.b * (0.04f + 0.05f * breathe), 1f);
                if (cube.light)
                    cube.light.intensity = intensity * 1.0f * breathe;
            }
            yield return null;
        }
    }

    IEnumerator LoopCracks()
    {
        while (true)
        {
            float t = Time.time;
            foreach (var cube in _cubes)
            {
                foreach (var crack in cube.cracks)
                {
                    // Crack energy pulse — flows along the crack segments
                    // Each segment lights up in sequence like electricity
                    int segCount = crack.segs.Count;
                    for (int si = 0; si < segCount; si++)
                    {
                        if (!crack.mats[si]) continue;

                        // Wave of brightness travelling along the crack
                        float wave = t * crack.speed + crack.phase;
                        float segT = si / (float)(segCount - 1);

                        // Primary pulse wave
                        float pulse1 = Mathf.Sin((wave - segT * 3f) * Mathf.PI);
                        // Secondary slower glow
                        float pulse2 = 0.3f + 0.3f * Mathf.Sin(wave * 0.4f + crack.phase);
                        float bright = Mathf.Clamp01(pulse1 * 0.6f + pulse2);

                        // When cube is hit, cracks flare — handled in HitEffect
                        if (cube.wasHit)
                        {
                            bright = 1f;
                        }

                        Color cc = cube.col;
                        cc.a = bright * (cube.wasHit ? 1f : 0.65f);
                        crack.mats[si].color = cc;

                        // Crack segments pulse slightly in scale with brightness
                        if (crack.segs[si])
                        {
                            float sc = 1f + 0.15f * bright;
                            crack.segs[si].localScale = new Vector3(
                                crack.segs[si].localScale.x * sc / (1f + 0.15f * (bright > 0 ? bright - Time.deltaTime * 2f : 0)),
                                crack.segs[si].localScale.y,
                                crack.segs[si].localScale.z);
                        }
                    }
                }
            }
            yield return null;
        }
    }

    IEnumerator LoopSparks()
    {
        while (true)
        {
            if (_cubes.Count > 0)
            {
                var cube = _cubes[Random.Range(0, _cubes.Count)];
                if (cube.rend)
                {
                    Bounds b = cube.rend.bounds;
                    Vector3 pos = b.center + new Vector3(
                        Random.Range(-b.extents.x * 0.7f, b.extents.x * 0.7f),
                        b.extents.y,
                        Random.Range(-b.extents.z * 0.7f, b.extents.z * 0.7f));
                    Vector3 vel = new Vector3(
                        Random.Range(-0.08f, 0.08f),
                        Random.Range(0.15f, 0.6f),
                        Random.Range(-0.08f, 0.08f));
                    Color col = cube.col; col.a = Random.Range(0.4f, 0.75f);
                    SpawnSpark(pos, vel, col, Random.Range(0.5f, 1.2f),
                        edgeWidth * Random.Range(0.3f, 0.7f));
                }
            }
            yield return new WaitForSeconds(Random.Range(0.35f, 1.0f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region HIT REACTION
    // ────────────────────────────────────────────────────────

    public void OnCubeHit(int cubeIndex, Vector3 contact)
    {
        if (cubeIndex < 0 || cubeIndex >= _cubes.Count) return;
        StartCoroutine(HitEffect(cubeIndex, contact));
    }

    IEnumerator HitEffect(int idx, Vector3 contact)
    {
        var cube = _cubes[idx];
        cube.wasHit = true;

        // Sharp sparks scatter
        for (int i = 0; i < Random.Range(8, 14); i++)
        {
            Vector3 dir = new Vector3(
                Random.Range(-1f, 1f), Random.Range(0.2f, 1.2f),
                Random.Range(-1f, 1f)).normalized;
            Color col = i % 3 == 0 ? Color.white : cube.col; col.a = 1f;
            SpawnSpark(contact, dir * Random.Range(1f, 3.5f), col,
                Random.Range(0.2f, 0.55f), edgeWidth * Random.Range(0.5f, 1.2f));
        }

        // Edges flare
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float p = 1f - t / 0.35f;
            Color fl = Color.Lerp(cube.col, Color.white, p * 0.7f);
            fl.a = 0.55f + 0.45f * p;
            foreach (var m in cube.edgeMats) if (m) m.color = fl;
            if (cube.light)
            {
                cube.light.color = fl;
                cube.light.intensity = intensity * (1f + 3.5f * p);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.12f);
        cube.wasHit = false;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SPARKS
    // ────────────────────────────────────────────────────────

    void SpawnSpark(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        size = Mathf.Max(size, 0.004f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "S"; Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(null);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var mat = new Material(Unlit()); mat.color = col;
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
            s.vel *= (1f - 1.2f * Time.deltaTime);
            s.vel.y -= 0.6f * Time.deltaTime;
            s.t.position += s.vel * Time.deltaTime;
            _sparks[i] = s;
            s.t.localScale = Vector3.one * s.sz;
            s.mat.color = new Color(s.col.r, s.col.g, s.col.b,
                Mathf.Max(s.col.a * Mathf.Pow(1f - prog, 1.2f), 0f));
        }
    }

    static Shader Unlit() =>
        Shader.Find("Universal Render Pipeline/Unlit")
        ?? Shader.Find("Unlit/Color")
        ?? Shader.Find("Standard");

    #endregion
}

// ============================================================
//  BoxHitReactor — auto-added per cube child
// ============================================================
public class BoxHitReactor : MonoBehaviour
{
    private BoxVisuals _parent;
    private int _index;

    public void Setup(BoxVisuals parent, int index)
    { _parent = parent; _index = index; }

    void OnCollisionEnter(Collision col)
    {
        if (!_parent) return;
        if (col.gameObject.CompareTag("Player1") ||
            col.gameObject.CompareTag("Player2") ||
            col.gameObject.CompareTag("Player3") ||
            col.gameObject.CompareTag("Player4"))
        {
            Vector3 pt = col.contacts.Length > 0
                ? col.contacts[0].point : transform.position;
            _parent.OnCubeHit(_index, pt);
        }
    }
}