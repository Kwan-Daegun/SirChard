using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  LEDLamp — Cinematic Arena Billboard (Interchanging Scroll)
// ============================================================

public class LEDLamp : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────
    [Header("Identity")]
    public int lampIndex = 0;
    public int totalLamps = 8;

    [Header("Light")]
    public Light pointLight;
    public float baseIntensity = 8f;
    public float lightRange = 15f;
    [Tooltip("Only enable on 2-3 lamps max — shadows are expensive!")]
    public bool castShadows = false;

    [Header("Base Colour")]
    public Color baseColor = new Color(0.00f, 0.88f, 1.00f, 1f);

    [Header("Show")]
    public bool autoShow = true;
    public float modeDuration = 15f;

    [Header("Images")]
    [Tooltip("Add your images here. They will interchange while scrolling.")]
    public Texture2D[] randomImages;
    public float scrollSpeed = 0.5f;

    [Header("Beam Sweep")]
    public bool showBeams = true;
    [Range(1f, 60f)] public float beamSweepSpeed = 8f;
    [Range(10f, 80f)] public float beamSweepRange = 40f;

    // ── Arena events ────────────────────
    public enum ArenaEvent { PlayerScored, Tackle, GameWon, GameLost, BallDropped }
    private static readonly List<LEDLamp> s_allLamps = new List<LEDLamp>();
    public static void BroadcastEvent(ArenaEvent ev)
    {
        foreach (var l in s_allLamps) l.OnArenaEvent(ev);
    }

    private enum ShowMode
    {
        Breathe, WaveSweep, StrobeBurst, RainbowFlow,
        Heartbeat, LaserScan, DataStream, EnergyCharge
    }

    private static readonly ShowMode[] s_modeOrder = {
        ShowMode.Breathe,    ShowMode.WaveSweep,   ShowMode.RainbowFlow,
        ShowMode.Heartbeat,  ShowMode.LaserScan,   ShowMode.DataStream,
        ShowMode.EnergyCharge, ShowMode.StrobeBurst
    };

    private static float s_time = 0f;
    private static ShowMode s_mode = ShowMode.Breathe;
    private static int s_modeIndex = 0;

    private Renderer _rend;
    private Material _mat;
    private Transform _beamA, _beamB;
    private Transform _halo;
    private Material _beamMatA, _beamMatB;
    private Material _haloMat;
    private bool _eventOverride = false;
    private float _textureOffset = 0f;

    private struct Particle
    {
        public Transform t;
        public MeshRenderer mr;
        public Material mat;
        public Vector3 vel;
        public Vector3 acc;
        public float born;
        public float life;
        public Color col;
        public float sz;
        public float rot;
        public float rotSpd;
    }
    private List<Particle> _particles = new List<Particle>();

    private Transform[] _playerTransforms;
    private MeshRenderer[][] _playerRenderers;
    private static readonly float SHADOW_HIDE_DIST = 3f;

    static readonly Color ColCyan = new Color(0.00f, 0.95f, 1.00f, 1f);
    static readonly Color ColViolet = new Color(0.70f, 0.10f, 1.00f, 1f);
    static readonly Color ColOrange = new Color(1.00f, 0.45f, 0.00f, 1f);
    static readonly Color ColRed = new Color(1.00f, 0.05f, 0.05f, 1f);
    static readonly Color ColGold = new Color(1.00f, 0.85f, 0.10f, 1f);
    static readonly Color ColWhite = new Color(1.00f, 1.00f, 1.00f, 1f);
    static readonly Color ColIce = new Color(0.80f, 0.95f, 1.00f, 1f);

    void Awake()
    {
        s_allLamps.Add(this);
        if (!pointLight) pointLight = GetComponentInChildren<Light>();
        _rend = GetComponent<Renderer>();
        if (_rend) _mat = _rend.material;
        if (pointLight)
        {
            pointLight.range = lightRange;
            pointLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        }

        BuildBeams();
        BuildHalo();

        if (!showBeams)
        {
            if (_beamA) _beamA.gameObject.SetActive(false);
            if (_beamB) _beamB.gameObject.SetActive(false);
            foreach (Transform child in transform)
            {
                if (child.name == "PivotA" || child.name == "PivotB")
                    child.gameObject.SetActive(false);
            }
        }
    }

    void OnDestroy() { s_allLamps.Remove(this); CleanParticles(); }

    void Start()
    {
        StartCoroutine(MasterShowLoop());
        StartCoroutine(ParticleEmitter());
        if (showBeams) StartCoroutine(SweepBeams());
        if (lampIndex == 0 && autoShow) StartCoroutine(ModeCycler());
        StartCoroutine(ProximityShadowLoop());

        ApplyRandomImage();
    }

    void Update()
    {
        s_time += Time.deltaTime;
        TickParticles();
        UpdateImageScroll();
    }

    #region IMAGE LOGIC
    void ApplyRandomImage()
    {
        if (randomImages == null || randomImages.Length == 0 || _mat == null) return;

        Texture2D selected = randomImages[Random.Range(0, randomImages.Length)];
        selected.wrapMode = TextureWrapMode.Repeat;

        string texProp = _mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        _mat.SetTexture(texProp, selected);

        // Ensure Tiling is 1,1 for standard scroll
        _mat.SetTextureScale(texProp, new Vector2(1, 1));

        if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 0);
    }

    void UpdateImageScroll()
    {
        if (_mat == null || randomImages.Length == 0) return;

        string texProp = _mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";

        // Scroll the texture
        _textureOffset -= Time.deltaTime * scrollSpeed;

        // If we have finished scrolling one full image (offset < -1), 
        // reset offset and pick a new random image
        if (_textureOffset <= -1f)
        {
            _textureOffset += 1f;
            ApplyRandomImage();
        }

        _mat.SetTextureOffset(texProp, new Vector2(_textureOffset, 0));
    }
    #endregion

    #region PROXIMITY SHADOW
    IEnumerator ProximityShadowLoop()
    {
        yield return new WaitForSeconds(0.5f);
        string[] tags = { "Player1", "Player2", "Player3", "Player4" };
        var playerList = new List<Transform>();
        var rendererList = new List<MeshRenderer[]>();

        foreach (var tag in tags)
        {
            var go = GameObject.FindGameObjectWithTag(tag);
            if (!go) continue;
            playerList.Add(go.transform);
            var mrs = new List<MeshRenderer>();
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                string n = mr.gameObject.name;
                if (n == "Halo" || n == "AuraRing" || n == "S" || n == "P" || n == "DizzyStar" || n == "Tr") continue;
                mrs.Add(mr);
            }
            rendererList.Add(mrs.ToArray());
        }
        _playerTransforms = playerList.ToArray();
        _playerRenderers = rendererList.ToArray();

        while (true)
        {
            foreach (var ledLamp in s_allLamps)
            {
                if (!ledLamp) continue;
                Vector3 lampPos = ledLamp.transform.position;
                for (int i = 0; i < _playerTransforms.Length; i++)
                {
                    if (_playerTransforms[i] == null) continue;
                    float dist = Vector3.Distance(_playerTransforms[i].position, lampPos);
                    bool nearLED = dist < SHADOW_HIDE_DIST;
                    foreach (var mr in _playerRenderers[i])
                    {
                        if (!mr) continue;
                        mr.shadowCastingMode = nearLED ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
            yield return new WaitForSeconds(0.05f);
        }
    }
    #endregion

    #region BUILD VISUALS
    void BuildBeams()
    {
        _beamMatA = MakeUnlitMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.0f));
        _beamMatB = MakeUnlitMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.0f));

        var pivotA = new GameObject("PivotA");
        pivotA.transform.SetParent(transform, false);
        pivotA.transform.localPosition = transform.InverseTransformPoint(transform.position + transform.right * 0.3f);
        _beamA = pivotA.transform;
        MakeCylinder("BeamA", pivotA.transform, Vector3.up * 4f, new Vector3(0.04f, 4f, 0.04f), _beamMatA);

        var pivotB = new GameObject("PivotB");
        pivotB.transform.SetParent(transform, false);
        pivotB.transform.localPosition = transform.InverseTransformPoint(transform.position - transform.right * 0.3f);
        _beamB = pivotB.transform;
        MakeCylinder("BeamB", pivotB.transform, Vector3.up * 4f, new Vector3(0.04f, 4f, 0.04f), _beamMatB);

        _beamA.localRotation = Quaternion.Euler(0f, 0f, -35f);
        _beamB.localRotation = Quaternion.Euler(0f, 0f, 35f);
    }

    IEnumerator SweepBeams()
    {
        float speedA = beamSweepSpeed + lampIndex * 0.5f;
        float speedB = beamSweepSpeed * 0.8f + lampIndex * 0.4f;
        float phaseA = lampIndex * 0.4f;
        float phaseB = lampIndex * 0.4f + Mathf.PI;

        while (true)
        {
            float angleA = Mathf.Sin(s_time * speedA * Mathf.Deg2Rad + phaseA) * beamSweepRange;
            float angleB = Mathf.Sin(s_time * speedB * Mathf.Deg2Rad + phaseB) * beamSweepRange;
            if (_beamA) _beamA.localRotation = Quaternion.Euler(0f, 0f, angleA);
            if (_beamB) _beamB.localRotation = Quaternion.Euler(0f, 0f, angleB);

            if (s_mode == ShowMode.EnergyCharge)
            {
                float chargePos = (s_time % 6f) / 6f;
                float spinSpeed = chargePos * 180f;
                if (_beamA) _beamA.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                if (_beamB) _beamB.Rotate(Vector3.up, -spinSpeed * Time.deltaTime, Space.World);
            }
            yield return null;
        }
    }

    void BuildHalo()
    {
        _haloMat = MakeUnlitMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0f));
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Halo";
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localPosition = Vector3.forward * 0.1f;
        Vector3 ps = transform.lossyScale;
        go.transform.localScale = new Vector3(0.5f * (1f / ps.x), 0.02f, 0.8f * (1f / ps.z));
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = _haloMat;
        _halo = go.transform;
    }

    Transform MakeCylinder(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos; go.transform.localScale = localScale;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().material = mat;
        return go.transform;
    }
    #endregion

    #region MASTER SHOW LOOP
    IEnumerator MasterShowLoop()
    {
        while (true)
        {
            if (_eventOverride) { yield return null; continue; }
            switch (s_mode)
            {
                case ShowMode.Breathe: yield return FrameBreathe(); break;
                case ShowMode.WaveSweep: yield return FrameWaveSweep(); break;
                case ShowMode.StrobeBurst: yield return FrameStrobe(); break;
                case ShowMode.RainbowFlow: yield return FrameRainbow(); break;
                case ShowMode.Heartbeat: yield return FrameHeartbeat(); break;
                case ShowMode.LaserScan: yield return FrameLaserScan(); break;
                case ShowMode.DataStream: yield return FrameDataStream(); break;
                case ShowMode.EnergyCharge: yield return FrameEnergyCharge(); break;
            }
            yield return null;
        }
    }

    IEnumerator ModeCycler()
    {
        while (true)
        {
            yield return new WaitForSeconds(modeDuration);
            s_modeIndex = (s_modeIndex + 1) % s_modeOrder.Length;
            s_mode = s_modeOrder[s_modeIndex];

            foreach (var l in s_allLamps)
            {
                // We keep the ApplyRandomImage logic within the scroll reset now
                // but let's force a random shuffle here too for variety
                l.ApplyRandomImage();
                l.StartCoroutine(l.FlashWhite());
            }
        }
    }

    IEnumerator FlashWhite()
    {
        SetAll(ColWhite, baseIntensity, 0.5f, 0.6f);
        yield return null;
    }
    #endregion

    #region SHOW FRAMES (STATIC & STABLE)
    IEnumerator FrameBreathe()
    {
        SetAll(baseColor, baseIntensity, 0.4f, 0.5f);
        yield return null;
    }

    IEnumerator FrameWaveSweep()
    {
        SetAll(baseColor, baseIntensity, 0.6f, 0.5f);
        yield return null;
    }

    IEnumerator FrameStrobe()
    {
        SetAll(ColWhite, baseIntensity, 0.9f, 0.9f);
        yield return null;
    }

    IEnumerator FrameRainbow()
    {
        float myOffset = lampIndex / (float)totalLamps;
        float hue = ((s_time * 0.15f) + myOffset) % 1f;
        Color col = Color.HSVToRGB(hue, 1f, 1f);
        SetAll(col, baseIntensity, 0.5f, 0.6f);
        yield return null;
    }

    IEnumerator FrameHeartbeat()
    {
        SetAll(ColViolet, baseIntensity, 0.7f, 0.8f);
        yield return null;
    }

    IEnumerator FrameLaserScan()
    {
        Color col = (((int)(s_time * 0.5f)) % 2 == 0) ? ColOrange : ColCyan;
        SetAll(col, baseIntensity, 0.85f, 0.7f);
        yield return null;
    }

    IEnumerator FrameDataStream()
    {
        SetAll(new Color(0f, 1f, 0.5f), baseIntensity, 0.3f, 0.4f);
        yield return null;
    }

    IEnumerator FrameEnergyCharge()
    {
        SetAll(ColWhite, baseIntensity, 0.8f, 0.9f);
        yield return null;
    }
    #endregion

    #region EVENTS & PARTICLES
    void OnArenaEvent(ArenaEvent ev)
    {
        switch (ev)
        {
            case ArenaEvent.PlayerScored: StartCoroutine(EventScored()); break;
            case ArenaEvent.Tackle: StartCoroutine(EventTackle()); break;
            case ArenaEvent.GameWon: StartCoroutine(EventWon()); break;
            case ArenaEvent.GameLost: StartCoroutine(EventLost()); break;
            case ArenaEvent.BallDropped: StartCoroutine(EventBallDrop()); break;
        }
    }

    IEnumerator EventScored()
    {
        _eventOverride = true;
        SetAll(ColGold, baseIntensity * 1.5f, 1f, 1f);
        yield return new WaitForSeconds(1.0f);
        _eventOverride = false;
    }

    IEnumerator EventTackle()
    {
        _eventOverride = true;
        SetAll(ColWhite, baseIntensity * 1.2f, 0.9f, 0.9f);
        yield return new WaitForSeconds(0.5f);
        _eventOverride = false;
    }

    IEnumerator EventWon()
    {
        _eventOverride = true;
        float endTime = s_time + 5f;
        while (s_time < endTime)
        {
            float hue = ((s_time * 0.4f) + (lampIndex / (float)totalLamps)) % 1f;
            SetAll(Color.HSVToRGB(hue, 1f, 1f), baseIntensity, 0.8f, 0.9f);
            yield return null;
        }
        _eventOverride = false;
    }

    IEnumerator EventLost()
    {
        _eventOverride = true;
        SetAll(ColRed, baseIntensity, 0.5f, 0.4f);
        yield return new WaitForSeconds(3.0f);
        _eventOverride = false;
    }

    IEnumerator EventBallDrop()
    {
        _eventOverride = true;
        SetAll(baseColor, baseIntensity, 0.5f, 0.6f);
        yield return new WaitForSeconds(0.5f);
        _eventOverride = false;
    }

    IEnumerator ParticleEmitter()
    {
        while (true)
        {
            if (pointLight && pointLight.intensity > baseIntensity * 0.4f)
                SpawnBurst(1, pointLight.color, 0.1f, 0.5f);
            yield return new WaitForSeconds(0.15f);
        }
    }

    void SpawnBurst(int count, Color col, float minSpeed, float maxSpeed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = transform.position + transform.right * Random.Range(-0.4f, 0.4f);
            SpawnParticle(pos, Vector3.up * Random.Range(minSpeed, maxSpeed), col, 0.6f, 0.05f);
        }
    }

    void SpawnParticle(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos; go.transform.localScale = Vector3.one * size;
        var mat = MakeUnlitMat(col);
        go.GetComponent<MeshRenderer>().material = mat;
        _particles.Add(new Particle { t = go.transform, mat = mat, vel = vel, born = Time.time, life = life, col = col, sz = size });
    }

    void TickParticles()
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            if (!p.t) { _particles.RemoveAt(i); continue; }
            float prog = (Time.time - p.born) / p.life;
            if (prog >= 1f) { Destroy(p.t.gameObject); _particles.RemoveAt(i); continue; }
            p.t.position += p.vel * Time.deltaTime;
            p.mat.color = new Color(p.col.r, p.col.g, p.col.b, 1f - prog);
            _particles[i] = p;
        }
    }

    void CleanParticles() { foreach (var p in _particles) if (p.t) Destroy(p.t.gameObject); _particles.Clear(); }

    void SetAll(Color col, float intensity, float beamAlpha, float haloAlpha)
    {
        if (pointLight) { pointLight.color = col; pointLight.intensity = intensity; }
        if (_mat)
        {
            _mat.DisableKeyword("_EMISSION");
            _mat.SetColor("_EmissionColor", Color.black);

            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", Color.white);
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", Color.white);
        }
        if (_beamMatA) _beamMatA.color = new Color(col.r, col.g, col.b, beamAlpha);
        if (_beamMatB) _beamMatB.color = new Color(col.r, col.g, col.b, beamAlpha * 0.7f);
        if (_haloMat) _haloMat.color = new Color(col.r, col.g, col.b, haloAlpha * 0.6f);
    }

    Material MakeUnlitMat(Color col)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.color = col; return mat;
    }
    #endregion
}