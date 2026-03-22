using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RubiksCubeVisuals — Living Rubik's Cube + Neon Edition
//
//  Add to the "Box" PARENT. Auto-finds all Cube children.
//
//  EFFECTS:
//  - 3x3x3 Rubik's cube replaces each box
//  - Layers rotate continuously
//  - Stickers cycle through vivid rainbow colours
//  - Neon strips on each outer cubie flicker on/off
//  - Centre point light pulses and colour-cycles
//  - Hit reaction — fast spin + spark burst
// ============================================================

public class RubiksCubeVisuals : MonoBehaviour
{
    [Header("Settings")]
    [Range(0.02f, 0.15f)] public float gapFraction = 0.06f;
    public float rotateSpeed = 180f;
    [Range(0.1f, 5f)] public float lightIntensity = 3f;

    // Face colours (initial — overridden by colour cycle)
    static readonly Color FaceTop = new Color(1.00f, 1.00f, 1.00f, 1f);
    static readonly Color FaceBottom = new Color(1.00f, 0.85f, 0.05f, 1f);
    static readonly Color FaceFront = new Color(0.00f, 0.75f, 1.00f, 1f);
    static readonly Color FaceBack = new Color(0.55f, 0.10f, 1.00f, 1f);
    static readonly Color FaceRight = new Color(1.00f, 0.35f, 0.05f, 1f);
    static readonly Color FaceLeft = new Color(0.15f, 0.85f, 0.25f, 1f);
    static readonly Color FaceInner = new Color(0.12f, 0.12f, 0.15f, 1f);

    private class RubikData
    {
        public Transform root;
        public Transform[,,] cubies = new Transform[3, 3, 3];
        public Material[,,] cubieMats = new Material[3, 3, 3];
        public List<Material> stickerMats = new List<Material>();
        public List<Material> neonMats = new List<Material>();
        public Light centreLight;
        public float phase;
        public Bounds originalBounds;
        public bool isHit;
        public bool rotating;
    }

    private List<RubikData> _rubiks = new List<RubikData>();

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
        StartCoroutine(LoopRotations());
        StartCoroutine(LoopLight());
        StartCoroutine(LoopColourCycle());
        StartCoroutine(LoopNeonFlicker());
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
            "Cubie","Sticker","Neon","BoxLight","S","Edge","Corner",
            "Halo","BeamA","BeamB","PivotA","PivotB","CrackSeg",
            "ScanLine","FaceGlow","Trace","CoreLight","TopBeam","RubikRoot"
        };
        int idx = 0;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr) continue;
            string n = mr.gameObject.name;
            if (skip.Contains(n) || n.Length <= 2) continue;
            if (!mr.GetComponent<Collider>()) continue;
            BuildRubik(mr, idx++);
        }
        Debug.Log($"[RubiksCubeVisuals] {_rubiks.Count} cubes built.");
    }

    void BuildRubik(MeshRenderer mr, int idx)
    {
        Bounds b = mr.bounds;
        Vector3 c = b.center;
        float sz = Mathf.Min(b.size.x, b.size.y, b.size.z);
        float cSz = sz / 3f;
        float net = cSz - cSz * gapFraction;

        mr.enabled = false;

        var root = new GameObject("RubikRoot_" + idx);
        root.transform.position = c;
        root.transform.rotation = Quaternion.identity;
        root.transform.SetParent(mr.transform, true);

        var rubik = new RubikData
        {
            root = root.transform,
            phase = idx * Mathf.PI * 0.7f,
            originalBounds = b,
            isHit = false,
            rotating = false
        };

        // Build 27 cubies
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                {
                    var cubie = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubie.name = "Cubie";
                    cubie.transform.SetParent(root.transform, false);
                    cubie.transform.localPosition = new Vector3((x - 1) * cSz, (y - 1) * cSz, (z - 1) * cSz);
                    cubie.transform.localScale = Vector3.one * net;
                    Destroy(cubie.GetComponent<Collider>());

                    var innerMat = new Material(Unlit()); innerMat.color = FaceInner;
                    cubie.GetComponent<MeshRenderer>().material = innerMat;
                    cubie.GetComponent<MeshRenderer>().shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.On;

                    rubik.cubies[x, y, z] = cubie.transform;
                    rubik.cubieMats[x, y, z] = innerMat;

                    // Face stickers
                    AddStickers(cubie.transform, x, y, z, net, rubik);

                    // Neon strips — only on outer cubies so they show on the surface
                    bool isOuter = x == 0 || x == 2 || y == 0 || y == 2 || z == 0 || z == 2;
                    if (isOuter) AddNeonStrips(cubie.transform, net, rubik);
                }

        // Centre light
        var lgo = new GameObject("BoxLight");
        lgo.transform.SetParent(root.transform, false);
        lgo.transform.localPosition = Vector3.zero;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.color = FaceFront;
        l.intensity = lightIntensity; l.range = sz * 3.5f; l.shadows = LightShadows.None;
        rubik.centreLight = l;

        var reactor = mr.gameObject.GetComponent<RubikHitReactor>();
        if (!reactor) reactor = mr.gameObject.AddComponent<RubikHitReactor>();
        reactor.Setup(this, _rubiks.Count);

        _rubiks.Add(rubik);
    }

    void AddStickers(Transform cubie, int x, int y, int z, float s, RubikData rubik)
    {
        float off = s * 0.505f;
        float size = s * 0.82f;
        float thick = s * 0.04f;
        if (y == 2) AddSticker(cubie, Vector3.up * off, new Vector3(size, thick, size), FaceTop, rubik);
        if (y == 0) AddSticker(cubie, Vector3.down * off, new Vector3(size, thick, size), FaceBottom, rubik);
        if (z == 2) AddSticker(cubie, Vector3.forward * off, new Vector3(size, size, thick), FaceFront, rubik);
        if (z == 0) AddSticker(cubie, Vector3.back * off, new Vector3(size, size, thick), FaceBack, rubik);
        if (x == 2) AddSticker(cubie, Vector3.right * off, new Vector3(thick, size, size), FaceRight, rubik);
        if (x == 0) AddSticker(cubie, Vector3.left * off, new Vector3(thick, size, size), FaceLeft, rubik);
    }

    void AddSticker(Transform parent, Vector3 lPos, Vector3 lScale, Color col, RubikData rubik)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Sticker";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lPos;
        go.transform.localScale = lScale;
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(Unlit()); mat.color = col;
        go.GetComponent<MeshRenderer>().material = mat;
        go.GetComponent<MeshRenderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        rubik.stickerMats.Add(mat);
    }

    void AddNeonStrips(Transform cubie, float cubieSize, RubikData rubik)
    {
        float h = cubieSize * 0.88f;
        float w = cubieSize * 0.018f;
        float off = cubieSize * 0.510f;

        var defs = new Vector3[]
        {
            new Vector3( off, 0f,  off),
            new Vector3(-off, 0f,  off),
            new Vector3( off, 0f, -off),
            new Vector3(-off, 0f, -off),
        };

        foreach (var pos in defs)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Neon";
            go.transform.SetParent(cubie, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(w, h * 0.5f, w);
            Destroy(go.GetComponent<Collider>());
            var mat = new Material(Unlit());
            mat.color = Color.black;
            go.GetComponent<MeshRenderer>().material = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            rubik.neonMats.Add(mat);
            // No per-neon lights — already at light budget limit
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region ROTATION
    // ────────────────────────────────────────────────────────

    IEnumerator LoopRotations()
    {
        yield return new WaitForSeconds(Random.Range(0.3f, 1.5f));
        while (true)
        {
            foreach (var rubik in _rubiks)
                if (!rubik.rotating && !rubik.isHit)
                    StartCoroutine(RotateSlice(rubik));
            yield return new WaitForSeconds(Random.Range(0.6f, 1.4f));
        }
    }

    IEnumerator RotateSlice(RubikData rubik)
    {
        rubik.rotating = true;
        int axis = Random.Range(0, 3);
        int slice = Random.Range(0, 3);
        float dir = Random.value > 0.5f ? 1f : -1f;
        float target = 90f * dir;

        var pivot = new GameObject("SlicePivot");
        pivot.transform.position = rubik.root.position;
        pivot.transform.rotation = rubik.root.rotation;

        var sliceCubies = new List<Transform>();
        for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
            {
                int x = axis == 0 ? slice : a;
                int y = axis == 1 ? slice : (axis == 0 ? a : b);
                int z = axis == 2 ? slice : b;
                if (x < 3 && y < 3 && z < 3)
                {
                    var cubie = rubik.cubies[x, y, z];
                    if (cubie) { cubie.SetParent(pivot.transform, true); sliceCubies.Add(cubie); }
                }
            }

        float rotated = 0f;
        float speed = rubik.isHit ? rotateSpeed * 5f : rotateSpeed;
        while (Mathf.Abs(rotated) < Mathf.Abs(target))
        {
            float step = speed * Time.deltaTime * dir;
            if (Mathf.Abs(rotated + step) > Mathf.Abs(target)) step = target - rotated;
            Vector3 ax3 = axis == 0 ? pivot.transform.right
                        : axis == 1 ? pivot.transform.up
                        : pivot.transform.forward;
            pivot.transform.Rotate(ax3, step, Space.World);
            rotated += step;
            yield return null;
        }

        foreach (var cubie in sliceCubies)
        {
            if (!cubie) continue;
            cubie.SetParent(rubik.root, true);
            Vector3 e = cubie.localEulerAngles;
            cubie.localEulerAngles = new Vector3(
                Mathf.Round(e.x / 90f) * 90f,
                Mathf.Round(e.y / 90f) * 90f,
                Mathf.Round(e.z / 90f) * 90f);
        }
        Destroy(pivot);
        rubik.rotating = false;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopLight()
    {
        Color[] cols = { FaceFront, FaceTop, FaceRight, FaceBack, FaceLeft };
        while (true)
        {
            float t = Time.time;
            foreach (var rubik in _rubiks)
            {
                if (!rubik.centreLight) continue;
                float pulse = 0.6f + 0.4f * Mathf.Sin(t * 1.2f + rubik.phase);
                rubik.centreLight.intensity = lightIntensity * pulse;
                int ci = (int)(t * 0.25f + rubik.phase) % cols.Length;
                rubik.centreLight.color = Color.Lerp(
                    cols[ci % cols.Length],
                    cols[(ci + 1) % cols.Length],
                    (t * 0.25f + rubik.phase) % 1f);
            }
            yield return null;
        }
    }

    IEnumerator LoopColourCycle()
    {
        // Each sticker slowly shifts through vivid rainbow hues
        while (true)
        {
            float t = Time.time;
            foreach (var rubik in _rubiks)
                for (int i = 0; i < rubik.stickerMats.Count; i++)
                {
                    var m = rubik.stickerMats[i]; if (!m) continue;
                    float hue = ((t * 0.8f) + rubik.phase * 0.15f + i * 0.04f) % 1f;
                    m.color = Color.HSVToRGB(hue, 1f, 1f);
                }
            yield return null;
        }
    }

    IEnumerator LoopNeonFlicker()
    {
        while (true)
        {
            float t = Time.time;
            foreach (var rubik in _rubiks)
            {
                for (int i = 0; i < rubik.neonMats.Count; i++)
                {
                    var m = rubik.neonMats[i]; if (!m) continue;

                    // Each tube has its own flicker personality based on index + phase
                    float slowWave = Mathf.Sin(t * 0.5f + i * 0.9f + rubik.phase);
                    float fastBuzz = Mathf.Sin(t * 18f + i * 2.1f + rubik.phase) * 0.12f;
                    float microBuzz = Mathf.Sin(t * 45f + i * 3.7f + rubik.phase) * 0.05f;

                    float bright;
                    if (slowWave < -0.65f)
                    {
                        // Fully OFF — neon tube dead
                        bright = 0f;
                    }
                    else if (slowWave < -0.35f)
                    {
                        // Struggling to turn on — rapid flicker
                        float struggle = Mathf.Abs(Mathf.Sin(t * 30f + i));
                        bright = struggle * 0.4f;
                    }
                    else
                    {
                        // ON with subtle buzz — like real neon hum
                        bright = Mathf.Clamp01(0.75f + fastBuzz + microBuzz);
                    }

                    // Neon hue matches sticker group but slightly offset
                    float hue = ((t * 0.8f) + rubik.phase * 0.15f + (i / 4) * 0.04f + 0.1f) % 1f;
                    Color col = Color.HSVToRGB(hue, 1f, bright);
                    m.color = col;
                }
            }
            yield return null;
        }
    }

    IEnumerator LoopSparks()
    {
        while (true)
        {
            if (_rubiks.Count > 0)
            {
                var rubik = _rubiks[Random.Range(0, _rubiks.Count)];
                Bounds b = rubik.originalBounds;
                float sz = Mathf.Min(b.size.x, b.size.y, b.size.z) * 0.5f;
                Vector3 pos = b.center + new Vector3(
                    Random.Range(-sz, sz), sz, Random.Range(-sz, sz));
                Vector3 vel = new Vector3(
                    Random.Range(-0.1f, 0.1f), Random.Range(0.2f, 0.7f),
                    Random.Range(-0.1f, 0.1f));
                Color[] sc = { FaceFront, FaceTop, FaceRight, Color.white };
                Color col = sc[Random.Range(0, sc.Length)]; col.a = Random.Range(0.5f, 0.9f);
                SpawnSpark(pos, vel, col, Random.Range(0.4f, 1f), 0.04f);
            }
            yield return new WaitForSeconds(Random.Range(0.4f, 1.2f));
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region HIT REACTION
    // ────────────────────────────────────────────────────────

    public void OnRubikHit(int index, Vector3 contact)
    {
        if (index < 0 || index >= _rubiks.Count) return;
        StartCoroutine(HitEffect(index, contact));
    }

    IEnumerator HitEffect(int idx, Vector3 contact)
    {
        var rubik = _rubiks[idx]; rubik.isHit = true;
        Color[] cols = { FaceFront, FaceTop, FaceRight, FaceLeft, Color.white };
        for (int i = 0; i < 20; i++)
        {
            Vector3 dir = new Vector3(
                Random.Range(-1f, 1f), Random.Range(0.2f, 1.5f),
                Random.Range(-1f, 1f)).normalized;
            Color col = cols[Random.Range(0, cols.Length)]; col.a = 1f;
            SpawnSpark(contact, dir * Random.Range(1.5f, 5f), col,
                Random.Range(0.3f, 0.8f), Random.Range(0.04f, 0.10f));
        }
        if (rubik.centreLight)
        { rubik.centreLight.intensity = lightIntensity * 5f; rubik.centreLight.color = Color.white; }

        // All neons flash white on hit
        foreach (var m in rubik.neonMats) if (m) m.color = Color.white;

        StartCoroutine(RotateSlice(rubik));
        StartCoroutine(RotateSlice(rubik));
        yield return new WaitForSeconds(0.25f);
        rubik.isHit = false;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SPARKS
    // ────────────────────────────────────────────────────────

    void SpawnSpark(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "S"; Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(null);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * Mathf.Max(size, 0.01f);
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
            s.vel.y -= 1.5f * Time.deltaTime;
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
//  RubikHitReactor — auto-added per cube child
// ============================================================
public class RubikHitReactor : MonoBehaviour
{
    private RubiksCubeVisuals _parent;
    private int _index;

    public void Setup(RubiksCubeVisuals parent, int index)
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
            _parent.OnRubikHit(_index, pt);
        }
    }
}