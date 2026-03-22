using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RubiksCubeVisuals — Fixed Edition
//
//  FIXES:
//  - Negative scale handled via Mathf.Abs on bounds size
//  - Cube array properly updated after each slice rotation
//    so it never loses track of where cubies are
//  - Minimum size fallback so even tiny-bounded cubes work
// ============================================================

public class RubiksCubeVisuals : MonoBehaviour
{
    [Header("Settings")]
    [Range(0.02f, 0.15f)] public float gapFraction = 0.06f;
    public float rotateSpeed = 180f;
    [Range(0.1f, 5f)] public float lightIntensity = 3f;
    public bool showColors = true; // uncheck to disable colour cycling

    static readonly Color FaceTop = new Color(1.00f, 1.00f, 1.00f, 1f);
    static readonly Color FaceBottom = new Color(1.00f, 0.85f, 0.05f, 1f);
    static readonly Color FaceFront = new Color(0.00f, 0.75f, 1.00f, 1f);
    static readonly Color FaceBack = new Color(0.55f, 0.10f, 1.00f, 1f);
    static readonly Color FaceRight = new Color(1.00f, 0.35f, 0.05f, 1f);
    static readonly Color FaceLeft = new Color(0.15f, 0.85f, 0.25f, 1f);
    static readonly Color FaceInner = new Color(0.12f, 0.12f, 0.15f, 1f);

    // ── Per-rubik data ────────────────────────────────────────
    private class RubikData
    {
        public Transform root;
        // Flat list — index = x*9 + y*3 + z, easier to update than 3D array
        public Transform[] cubies = new Transform[27];
        public List<Material> stickerMats = new List<Material>();
        public List<Material> neonMats = new List<Material>();
        public Light centreLight;
        public float phase;
        public Vector3 cubeCenter;   // world-space centre
        public float cubeSize;     // world-space size (always positive)
        public bool isHit;
        public bool rotating;
    }

    private int CI(int x, int y, int z) => x * 9 + y * 3 + z;

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
        if (showColors)
        {
            StartCoroutine(LoopColourCycle());
            StartCoroutine(LoopNeonFlicker());
        }
        StartCoroutine(LoopSparks());
        StartCoroutine(EnforceShadowsAfterBuild());
    }

    void Update() => TickSparks();

    void OnDestroy()
    {
        foreach (var s in _sparks) if (s.t) Destroy(s.t.gameObject);
        foreach (var r in _rubiks) if (r.root) Destroy(r.root.gameObject);
    }

    // ── Force shadows ON after everything is built ───────────
    IEnumerator EnforceShadowsAfterBuild()
    {
        // Wait for all Start() methods to finish
        yield return new WaitForSeconds(0.2f);

        // Force every Cubie to cast shadows
        foreach (var rubik in _rubiks)
        {
            foreach (var cubie in rubik.cubies)
            {
                if (!cubie) continue;
                var mr = cubie.GetComponent<MeshRenderer>();
                if (mr)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
                // Also force all children (stickers, neons)
                foreach (var child in cubie.GetComponentsInChildren<MeshRenderer>())
                {
                    if (child.gameObject.name == "Sticker")
                    {
                        child.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        child.receiveShadows = false;
                    }
                }
            }
        }

        // Force Ground/floor to receive shadows
        var allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var mr in allRenderers)
        {
            if (!mr) continue;
            string n = mr.gameObject.name;
            if (n == "Ground" || n.ToLower().Contains("ground") ||
                n == "Floor" || n.ToLower().Contains("floor"))
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
        }

        Debug.Log("[RubiksCubeVisuals] Shadows enforced on all cubies and floor.");
    }

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void FindAndBuild()
    {
        var skip = new HashSet<string> {
            "Cubie","Sticker","Neon","BoxLight","S","Edge","Corner",
            "Halo","BeamA","BeamB","PivotA","PivotB","CrackSeg",
            "ScanLine","FaceGlow","Trace","CoreLight","TopBeam","RubikRoot",
            "SlicePivot","NL","PanelDot","SpawnDisc","CentreOrb","FloorRing"
        };
        int idx = 0;
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr) continue;
            string n = mr.gameObject.name;
            if (skip.Contains(n)) { Debug.Log($"[Rubik] SKIP(list): {n}"); continue; }
            if (n.StartsWith("Rubik")) { Debug.Log($"[Rubik] SKIP(rubik): {n}"); continue; }
            if (!mr.GetComponent<Collider>()) { Debug.Log($"[Rubik] SKIP(nocol): {n}"); continue; }
            Debug.Log($"[Rubik] BUILDING: {n} scale={mr.transform.lossyScale}");
            BuildRubik(mr, idx++);
        }
        Debug.Log($"[RubiksCubeVisuals] {_rubiks.Count} cubes built.");
    }

    void BuildRubik(MeshRenderer mr, int idx)
    {
        Bounds b = mr.bounds;
        Vector3 c = b.center;
        Vector3 ls = mr.transform.lossyScale;

        // bounds.size is reliable for positive scale but wrong for negative
        // Use Abs of bounds extents * 2 which Unity computes correctly
        float bx = Mathf.Abs(b.extents.x) * 2f;
        float by = Mathf.Abs(b.extents.y) * 2f;
        float bz = Mathf.Abs(b.extents.z) * 2f;
        float sz = Mathf.Min(bx, by, bz);
        // If bounds are broken (near zero), fall back to lossyScale * localBounds
        if (sz < 0.01f)
        {
            Vector3 lb = mr.localBounds.size;
            sz = Mathf.Min(
                Mathf.Abs(ls.x) * lb.x,
                Mathf.Abs(ls.y) * lb.y,
                Mathf.Abs(ls.z) * lb.z);
        }
        if (sz < 0.01f) sz = 1f; // last resort fallback

        float cSz = sz / 3f;
        float net = cSz * (1f - gapFraction);

        Debug.Log($"[RubiksCubeVisuals] Cube {idx}: lossyScale={ls} sz={sz:F2} cSz={cSz:F2} net={net:F2} center={c}");

        mr.enabled = false;

        var root = new GameObject("RubikRoot_" + idx);
        root.transform.position = c;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var rubik = new RubikData
        {
            root = root.transform,
            phase = idx * Mathf.PI * 0.7f,
            cubeCenter = c,
            cubeSize = sz,
            isHit = false,
            rotating = false
        };

        // Build 27 cubies — root has clean 1,1,1 scale so localScale works fine
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 3; z++)
                {
                    var cubie = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubie.name = "Cubie";
                    cubie.transform.SetParent(root.transform, false);
                    cubie.transform.localPosition = new Vector3((x - 1) * cSz, (y - 1) * cSz, (z - 1) * cSz);
                    cubie.transform.localScale = Vector3.one * net;
                    cubie.transform.localRotation = Quaternion.identity;
                    Destroy(cubie.GetComponent<Collider>());

                    var innerMat = new Material(Unlit()); innerMat.color = FaceInner;
                    var cubieMR = cubie.GetComponent<MeshRenderer>();
                    cubieMR.material = innerMat;
                    cubieMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    cubieMR.receiveShadows = true;

                    rubik.cubies[CI(x, y, z)] = cubie.transform;

                    AddStickers(cubie.transform, x, y, z, net, rubik);
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

        // Real shadows come from directional light — no fake quad needed
        // ShadowForcer handles forcing Cast Shadows on all cubies

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
        go.transform.localRotation = Quaternion.identity;
        Destroy(go.GetComponent<Collider>());
        var mat = new Material(Unlit());
        mat.color = showColors ? col : FaceInner; // dark if colors off
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
        var defs = new Vector3[] {
            new Vector3( off,0f, off), new Vector3(-off,0f, off),
            new Vector3( off,0f,-off), new Vector3(-off,0f,-off),
        };
        foreach (var pos in defs)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Neon";
            go.transform.SetParent(cubie, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(w, h * 0.5f, w);
            go.transform.localRotation = Quaternion.identity;
            Destroy(go.GetComponent<Collider>());
            var mat = new Material(Unlit()); mat.color = Color.black;
            go.GetComponent<MeshRenderer>().material = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            rubik.neonMats.Add(mat);
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region ROTATION — FIXED ARRAY TRACKING
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

        // Collect the 9 cubies in this slice
        var sliceIndices = new List<(int x, int y, int z)>();
        var sliceCubies = new List<Transform>();

        for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
            {
                int x = axis == 0 ? slice : a;
                int y = axis == 1 ? slice : (axis == 0 ? a : b);
                int z = axis == 2 ? slice : b;
                if (x < 3 && y < 3 && z < 3)
                {
                    var cubie = rubik.cubies[CI(x, y, z)];
                    if (cubie)
                    {
                        sliceIndices.Add((x, y, z));
                        sliceCubies.Add(cubie);
                    }
                }
            }

        // Parent to pivot for rotation
        var pivot = new GameObject("SlicePivot");
        pivot.transform.position = rubik.root.position;
        pivot.transform.rotation = rubik.root.rotation;
        foreach (var cubie in sliceCubies)
            cubie.SetParent(pivot.transform, true);

        // Rotate smoothly
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

        // Re-parent and snap rotations
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

        // ── FIX 2: Update the cubies array to reflect new positions ──
        // After rotation, the logical positions change — remap the array
        // by recalculating which grid slot each cubie now occupies
        UpdateCubiesArray(rubik);

        // Re-enforce shadows after reparenting — Unity can reset these
        foreach (var cubie in sliceCubies)
        {
            if (!cubie) continue;
            var mr = cubie.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }

        rubik.rotating = false;
    }

    void UpdateCubiesArray(RubikData rubik)
    {
        // Re-map cubies array based on current world positions
        // Each cubie should map to the nearest grid slot

        float sz = rubik.cubeSize;
        float cSz = sz / 3f;

        // Collect all 27 cubie transforms
        var allCubies = new List<Transform>();
        foreach (var c in rubik.cubies) if (c) allCubies.Add(c);

        // Clear array
        for (int i = 0; i < 27; i++) rubik.cubies[i] = null;

        foreach (var cubie in allCubies)
        {
            if (!cubie) continue;

            // Get local position relative to root
            Vector3 local = rubik.root.InverseTransformPoint(cubie.position);

            // Round to nearest grid slot
            int gx = Mathf.RoundToInt(local.x / cSz) + 1;
            int gy = Mathf.RoundToInt(local.y / cSz) + 1;
            int gz = Mathf.RoundToInt(local.z / cSz) + 1;

            // Clamp to 0-2 range
            gx = Mathf.Clamp(gx, 0, 2);
            gy = Mathf.Clamp(gy, 0, 2);
            gz = Mathf.Clamp(gz, 0, 2);

            rubik.cubies[CI(gx, gy, gz)] = cubie;
        }
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
                    float slowWave = Mathf.Sin(t * 0.5f + i * 0.9f + rubik.phase);
                    float fastBuzz = Mathf.Sin(t * 18f + i * 2.1f + rubik.phase) * 0.12f;
                    float microBuzz = Mathf.Sin(t * 45f + i * 3.7f + rubik.phase) * 0.05f;
                    float bright;
                    if (slowWave < -0.65f) bright = 0f;
                    else if (slowWave < -0.35f) bright = Mathf.Abs(Mathf.Sin(t * 30f + i)) * 0.4f;
                    else bright = Mathf.Clamp01(0.75f + fastBuzz + microBuzz);
                    float hue = ((t * 0.8f) + rubik.phase * 0.15f + (i / 4) * 0.04f + 0.1f) % 1f;
                    m.color = Color.HSVToRGB(hue, 1f, bright);
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
                float sz = rubik.cubeSize * 0.5f;
                Vector3 pos = rubik.cubeCenter + new Vector3(
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