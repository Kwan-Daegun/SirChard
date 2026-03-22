using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  LEDLamp — Cinematic Arena Billboard
//
//  This is a FULL light show. Each LED panel:
//  - Runs coordinated light shows synced across all panels
//  - Shoots volumetric-style light beams into the air
//  - Fires particle cannons (confetti, sparks, energy bolts)
//  - Reacts to gameplay events (score, tackle, win, lose)
//  - Has 8 distinct show modes that flow cinematically
//  - Emits a visible "glow halo" around the panel face
//
//  SETUP:
//  1. Add to LEDLamp GameObject (parent cube)
//  2. Set Lamp Index (0–7) on each lamp
//  3. Set Total Lamps to match how many you placed
//  4. Each lamp auto-finds its Point Light child
//
//  CALL FROM OTHER SCRIPTS:
//  LEDLamp.BroadcastEvent(LEDLamp.ArenaEvent.PlayerScored);
//  LEDLamp.BroadcastEvent(LEDLamp.ArenaEvent.GameWon);
//  LEDLamp.BroadcastEvent(LEDLamp.ArenaEvent.Tackle);
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

    [Header("Beam Sweep")]
    public bool showBeams = true;             // uncheck for floor LEDs
    [Range(1f, 60f)] public float beamSweepSpeed = 8f;
    [Range(10f, 80f)] public float beamSweepRange = 40f;

    // ── Arena events (call from anywhere) ────────────────────
    public enum ArenaEvent { PlayerScored, Tackle, GameWon, GameLost, BallDropped }
    private static readonly List<LEDLamp> s_allLamps = new List<LEDLamp>();
    public static void BroadcastEvent(ArenaEvent ev)
    {
        foreach (var l in s_allLamps) l.OnArenaEvent(ev);
    }

    // ── Show modes ───────────────────────────────────────────
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

    // ── Shared global state ───────────────────────────────────
    private static float s_time = 0f;
    private static ShowMode s_mode = ShowMode.Breathe;
    private static int s_modeIndex = 0;
    private static float s_modeSince = 0f;

    // ── Per-lamp state ────────────────────────────────────────
    private Renderer _rend;
    private Material _mat;
    private Transform _beamA, _beamB;         // two "beam" cylinders above panel
    private Transform _halo;                   // flat glow disc on panel face
    private Material _beamMatA, _beamMatB;
    private Material _haloMat;
    private bool _eventOverride = false;

    // ── Particles ────────────────────────────────────────────
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

    // ── Colour palette ───────────────────────────────────────
    static readonly Color ColCyan = new Color(0.00f, 0.95f, 1.00f, 1f);
    static readonly Color ColViolet = new Color(0.70f, 0.10f, 1.00f, 1f);
    static readonly Color ColOrange = new Color(1.00f, 0.45f, 0.00f, 1f);
    static readonly Color ColLime = new Color(0.20f, 1.00f, 0.20f, 1f);
    static readonly Color ColRed = new Color(1.00f, 0.05f, 0.05f, 1f);
    static readonly Color ColGold = new Color(1.00f, 0.85f, 0.10f, 1f);
    static readonly Color ColWhite = new Color(1.00f, 1.00f, 1.00f, 1f);
    static readonly Color ColIce = new Color(0.80f, 0.95f, 1.00f, 1f);

    // ────────────────────────────────────────────────────────
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

        // Hide beams entirely for floor LEDs
        if (!showBeams)
        {
            if (_beamA) _beamA.gameObject.SetActive(false);
            if (_beamB) _beamB.gameObject.SetActive(false);
            // Also hide beam children
            foreach (Transform child in transform)
            {
                if (child.name == "PivotA" || child.name == "PivotB")
                    child.gameObject.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        s_allLamps.Remove(this);
        CleanParticles();
    }

    void Start()
    {
        StartCoroutine(MasterShowLoop());
        StartCoroutine(ParticleEmitter());
        if (showBeams) StartCoroutine(SweepBeams());
        if (lampIndex == 0 && autoShow) StartCoroutine(ModeCycler());
    }

    void Update()
    {
        s_time += Time.deltaTime;
        TickParticles();
    }

    // ────────────────────────────────────────────────────────
    #region BUILD VISUAL OBJECTS
    // ────────────────────────────────────────────────────────

    void BuildBeams()
    {
        _beamMatA = MakeUnlitMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.0f));
        _beamMatB = MakeUnlitMat(new Color(baseColor.r, baseColor.g, baseColor.b, 0.0f));

        // ── Beam A — pivot at base, sweeps left/right ────────
        // We put the cylinder INSIDE a pivot object so rotation
        // happens around the lamp base, not the cylinder centre.
        var pivotA = new GameObject("PivotA");
        pivotA.transform.SetParent(transform, false);
        pivotA.transform.localPosition = transform.InverseTransformPoint(
            transform.position + transform.right * 0.3f);
        _beamA = pivotA.transform;

        var cylA = MakeCylinder("BeamA", pivotA.transform,
            Vector3.up * 4f,              // cylinder top is 4 units up in pivot space
            new Vector3(0.04f, 4f, 0.04f),
            _beamMatA);

        // ── Beam B — pivot at base, sweeps opposite phase ────
        var pivotB = new GameObject("PivotB");
        pivotB.transform.SetParent(transform, false);
        pivotB.transform.localPosition = transform.InverseTransformPoint(
            transform.position - transform.right * 0.3f);
        _beamB = pivotB.transform;

        var cylB = MakeCylinder("BeamB", pivotB.transform,
            Vector3.up * 4f,
            new Vector3(0.04f, 4f, 0.04f),
            _beamMatB);

        // Start beams at different angles so they cross dramatically
        _beamA.localRotation = Quaternion.Euler(0f, 0f, -35f);
        _beamB.localRotation = Quaternion.Euler(0f, 0f, 35f);

        // Kick off the sweep coroutine
        StartCoroutine(SweepBeams());
    }

    IEnumerator SweepBeams()
    {
        // Each lamp gets a slightly different sweep speed and range
        // so they don't all move identically
        float speedA = beamSweepSpeed + lampIndex * 0.5f;
        float speedB = beamSweepSpeed * 0.8f + lampIndex * 0.4f;
        float rangeA = beamSweepRange;
        float rangeB = beamSweepRange * 1.1f;
        float phaseA = lampIndex * 0.4f;              // stagger per lamp
        float phaseB = lampIndex * 0.4f + Mathf.PI;  // B sweeps opposite

        while (true)
        {
            float t = s_time;

            // Smooth sine sweep — feels like a real searchlight
            float angleA = Mathf.Sin(t * speedA * Mathf.Deg2Rad + phaseA) * rangeA;
            float angleB = Mathf.Sin(t * speedB * Mathf.Deg2Rad + phaseB) * rangeB;

            // Rotate on Z axis = sweeps LEFT and RIGHT relative to the panel face
            if (_beamA) _beamA.localRotation = Quaternion.Euler(0f, 0f, angleA);
            if (_beamB) _beamB.localRotation = Quaternion.Euler(0f, 0f, angleB);

            // During energy charge mode — beams spin faster and wider
            if (s_mode == ShowMode.EnergyCharge)
            {
                float chargePos = (s_time % 6f) / 6f;
                float spinSpeed = chargePos * 180f;
                if (_beamA) _beamA.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                if (_beamB) _beamB.Rotate(Vector3.up, -spinSpeed * Time.deltaTime, Space.World);
            }

            // During strobe — freeze beams at crossed position
            if (s_mode == ShowMode.StrobeBurst)
            {
                if (_beamA) _beamA.localRotation = Quaternion.Euler(0f, 0f, -45f);
                if (_beamB) _beamB.localRotation = Quaternion.Euler(0f, 0f, 45f);
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

        // Compensate for parent scale so halo is always ~0.5 world units wide
        Vector3 ps = transform.lossyScale;
        float invX = ps.x > 0.001f ? 1f / ps.x : 1f;
        float invZ = ps.z > 0.001f ? 1f / ps.z : 1f;
        go.transform.localScale = new Vector3(0.5f * invX, 0.02f, 0.8f * invZ);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = _haloMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _halo = go.transform;
    }

    Transform MakeCylinder(string name, Transform parent, Vector3 localPos,
        Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        Destroy(go.GetComponent<Collider>());
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region MASTER SHOW LOOP
    // ────────────────────────────────────────────────────────

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
        // Only lamp 0 drives the global mode so all lamps sync
        while (true)
        {
            yield return new WaitForSeconds(modeDuration);
            s_modeIndex = (s_modeIndex + 1) % s_modeOrder.Length;
            s_mode = s_modeOrder[s_modeIndex];

            // Announce mode change with a quick white flash on all lamps
            foreach (var l in s_allLamps) l.StartCoroutine(l.FlashWhite());
        }
    }

    IEnumerator FlashWhite()
    {
        SetAll(ColWhite, baseIntensity * 2.5f, 0.8f, 0f);
        yield return new WaitForSeconds(0.06f);
        SetAll(baseColor, 0f, 0f, 0f);
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region SHOW FRAMES (called every frame from MasterShowLoop)
    // ────────────────────────────────────────────────────────

    // ── BREATHE — slow cosmic pulse ───────────────────────────
    IEnumerator FrameBreathe()
    {
        float myPhase = (lampIndex / (float)totalLamps) * Mathf.PI * 2f;
        float t = s_time;

        // Primary slow breathe
        float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.6f + myPhase * 0.3f);
        // Secondary fast shimmer
        float shimmer = 0.85f + 0.15f * Mathf.Sin(t * 4.2f + myPhase);
        float combined = breathe * shimmer;

        // Colour shifts from cyan → icy white → cyan as it breathes
        Color col = Color.Lerp(
            new Color(baseColor.r * 0.3f, baseColor.g * 0.4f, baseColor.b * 0.5f),
            ColIce, combined);

        float intensity = baseIntensity * combined;
        float beamAlpha = combined * 0.35f;
        float haloAlpha = combined * 0.55f;

        SetAll(col, intensity, beamAlpha, haloAlpha);
        yield return null;
    }

    // ── WAVE SWEEP — chase wave flows around arena ────────────
    IEnumerator FrameWaveSweep()
    {
        // Multiple overlapping waves at different speeds
        float myAngle = (lampIndex / (float)totalLamps) * 360f;
        float wave1 = Mathf.Sin((s_time * 120f - myAngle) * Mathf.Deg2Rad);
        float wave2 = Mathf.Sin((s_time * 80f + myAngle) * Mathf.Deg2Rad);
        float combined = Mathf.Clamp01((wave1 + wave2 + 0.3f) * 0.6f);

        // Colour alternates: leading edge = white, trailing = deep colour
        Color col = Color.Lerp(
            new Color(baseColor.r * 0.05f, baseColor.g * 0.05f, baseColor.b * 0.15f),
            ColWhite, combined);
        col = Color.Lerp(col, baseColor, 1f - combined);

        SetAll(col, baseIntensity * combined, combined * 0.6f, combined * 0.5f);

        // Shoot sparks from the brightest lamps
        if (combined > 0.8f && Random.value < 0.15f)
            SpawnBurst(3, baseColor, 0.5f, 1.2f);

        yield return null;
    }

    // ── STROBE BURST — synchronized explosive strobe ──────────
    IEnumerator FrameStrobe()
    {
        // Each lamp has a staggered phase so the strobe ripples outward
        float myPhase = lampIndex / (float)totalLamps;
        float strobeT = (s_time * 6f + myPhase) % 1f;

        bool isOn = strobeT < 0.08f;
        Color col = isOn ? ColWhite : Color.black;
        float intens = isOn ? baseIntensity * 3f : 0f;
        float beamAlpha = isOn ? 0.9f : 0f;

        SetAll(col, intens, beamAlpha, isOn ? 1f : 0f);

        // Strobe bursts — massive particle eruption when on
        if (isOn && strobeT < 0.02f)
            SpawnBurst(8, ColWhite, 1.5f, 2f);

        yield return null;
    }

    // ── RAINBOW FLOW — hue flows around the ring ──────────────
    IEnumerator FrameRainbow()
    {
        float myOffset = lampIndex / (float)totalLamps;
        float hue = ((s_time * 0.15f) + myOffset) % 1f;
        Color col = Color.HSVToRGB(hue, 1f, 1f);

        float pulse = 0.7f + 0.3f * Mathf.Sin(s_time * 2f + myOffset * Mathf.PI * 2f);
        SetAll(col, baseIntensity * pulse, pulse * 0.5f, pulse * 0.6f);

        // Confetti-style coloured sparks
        if (Random.value < 0.08f)
        {
            Color sparkCol = Color.HSVToRGB(Random.value, 0.9f, 1f);
            SpawnBurst(2, sparkCol, 0.8f, 1.5f);
        }

        yield return null;
    }

    // ── HEARTBEAT — arena-wide double thump ───────────────────
    IEnumerator FrameHeartbeat()
    {
        // Heartbeat timing — BPM ~72
        float bpm = 72f;
        float beat = (s_time * bpm / 60f) % 1f;
        float thump1 = Mathf.Exp(-beat * 18f);            // first thump
        float thump2 = Mathf.Exp(-(beat - 0.18f) * 22f);  // second thump (softer)
        float combined = Mathf.Clamp01(thump1 + thump2 * 0.6f);

        // Ripple outward from lamp 0 → lamp N
        float delay = (lampIndex / (float)totalLamps) * 0.12f;
        float ripple = combined; // could add delay here with phase

        Color col = Color.Lerp(
            new Color(0.4f, 0f, 0.1f),
            new Color(1f, 0.2f, 0.3f),
            ripple);

        SetAll(col, baseIntensity * (0.05f + 1.8f * ripple),
            ripple * 0.7f, ripple * 0.8f);

        // On strong beat — shoot sparks upward
        if (ripple > 0.85f && Random.value < 0.2f)
            SpawnBurst(5, new Color(1f, 0.3f, 0.3f), 1f, 2f);

        yield return null;
    }

    // ── LASER SCAN — single sweeping beam ────────────────────
    IEnumerator FrameLaserScan()
    {
        float myAngle = (lampIndex / (float)totalLamps) * 360f;
        float scanAngle = (s_time * 45f) % 360f; // 45 deg/sec

        float diff = Mathf.Abs(Mathf.DeltaAngle(scanAngle, myAngle));
        // Sharp falloff — only the lamp closest to scan angle is bright
        float glow = Mathf.Exp(-diff * diff * 0.008f);

        // Alternating scan colour
        bool warm = ((int)(s_time * 0.5f)) % 2 == 0;
        Color col = warm ? ColOrange : ColCyan;

        SetAll(col, baseIntensity * glow * 2.5f,
            glow * 0.85f, glow * 0.7f);

        // Trail particles from the scan head
        if (glow > 0.7f && Random.value < 0.25f)
        {
            SpawnBurst(3, col, 0.6f, 1f);
            // Also fire a line of particles along the beam direction
            for (int i = 0; i < 3; i++)
            {
                Vector3 up = transform.up * (1f + i * 0.8f);
                Vector3 vel = transform.forward * Random.Range(0.1f, 0.3f)
                            + transform.up * Random.Range(0.5f, 1.5f);
                SpawnParticle(transform.position + up, vel, col,
                    Random.Range(0.4f, 0.9f), 0.06f);
            }
        }

        yield return null;
    }

    // ── DATA STREAM — digital rain-style effect ───────────────
    IEnumerator FrameDataStream()
    {
        // Base low glow
        float baseGlow = 0.15f + 0.05f * Mathf.Sin(s_time * 3f);
        Color baseCol = new Color(0f, 0.3f, 0.15f);
        SetAll(baseCol, baseIntensity * baseGlow, 0f, baseGlow * 0.3f);

        // Random bright flashes — like data packets
        float myPhase = lampIndex * 0.7f;
        float packet = Mathf.Max(0f, Mathf.Sin(s_time * 3.5f + myPhase));
        if (packet > 0.85f)
        {
            Color dataCol = new Color(0f, 1f, 0.5f);
            float intensity = baseIntensity * packet * 1.5f;
            SetAll(dataCol, intensity, packet * 0.5f, packet * 0.5f);

            // Stream particles downward
            if (Random.value < 0.3f)
            {
                Vector3 vel = -transform.up * Random.Range(0.3f, 1f)
                            + transform.forward * 0.1f;
                SpawnParticle(
                    transform.position + transform.up * Random.Range(0f, 0.5f),
                    vel, dataCol, Random.Range(0.2f, 0.5f),
                    Random.Range(0.03f, 0.07f));
            }
        }

        yield return null;
    }

    // ── ENERGY CHARGE — builds to massive release ─────────────
    IEnumerator FrameEnergyCharge()
    {
        float cycleLen = 6f; // seconds for one charge→release
        float pos = (s_time % cycleLen) / cycleLen;

        if (pos < 0.7f)
        {
            // CHARGE PHASE — intensifies with flickers
            float charge = pos / 0.7f;
            float flicker = 1f + 0.2f * Mathf.Sin(s_time * (5f + charge * 20f));
            Color col = Color.Lerp(ColViolet, ColWhite, charge);

            // Sparks increasingly fly toward the panel as it charges
            if (Random.value < charge * 0.3f)
            {
                // Inward-flying spark (toward panel face)
                Vector3 startPos = transform.position
                    + transform.forward * Random.Range(-2f, -0.5f)
                    + new Vector3(Random.Range(-0.4f, 0.4f),
                                  Random.Range(-0.2f, 0.2f), 0f);
                Vector3 vel = transform.forward * Random.Range(0.5f, 1.5f);
                SpawnParticle(startPos, vel, col, 0.3f, 0.05f);
            }

            SetAll(col, baseIntensity * (0.2f + 0.8f * charge) * flicker,
                charge * 0.4f, charge * 0.6f);
        }
        else
        {
            // RELEASE PHASE — explosive burst
            float releaseT = (pos - 0.7f) / 0.3f;

            if (releaseT < 0.05f) // the moment of release
            {
                SetAll(ColWhite, baseIntensity * 4f, 1f, 1f);
                SpawnBurst(20, ColWhite, 3f, 4f);
                SpawnBurst(10, ColViolet, 2f, 3f);
                SpawnBurst(8, baseColor, 2.5f, 3.5f);
            }

            // Decay rapidly
            float decay = 1f - releaseT;
            Color col = Color.Lerp(baseColor, ColWhite, decay);
            SetAll(col, baseIntensity * decay * 2f, decay * 0.8f, decay * 0.9f);
        }

        yield return null;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region ARENA EVENTS
    // ────────────────────────────────────────────────────────

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
        float myPhase = lampIndex / (float)totalLamps;
        yield return new WaitForSeconds(myPhase * 0.15f); // stagger outward

        // Gold burst
        SetAll(ColGold, baseIntensity * 3f, 1f, 1f);
        SpawnBurst(15, ColGold, 2f, 3f);
        SpawnBurst(8, ColWhite, 2f, 2.5f);
        yield return new WaitForSeconds(0.15f);

        // Rapid rainbow flash
        for (int i = 0; i < 6; i++)
        {
            Color c = Color.HSVToRGB(i / 6f, 1f, 1f);
            SetAll(c, baseIntensity * 2f, 0.6f, 0.7f);
            yield return new WaitForSeconds(0.08f);
        }
        _eventOverride = false;
    }

    IEnumerator EventTackle()
    {
        _eventOverride = true;
        // Sharp white slam
        SetAll(ColWhite, baseIntensity * 2.5f, 0.9f, 0.9f);
        SpawnBurst(6, ColOrange, 1.5f, 2.5f);
        yield return new WaitForSeconds(0.05f);
        SetAll(ColOrange, baseIntensity * 0.5f, 0.1f, 0.2f);
        yield return new WaitForSeconds(0.3f);
        _eventOverride = false;
    }

    IEnumerator EventWon()
    {
        _eventOverride = true;
        float endTime = s_time + 8f;
        while (s_time < endTime)
        {
            // Celebratory rainbow loop
            float myOffset = lampIndex / (float)totalLamps;
            float hue = ((s_time * 0.4f) + myOffset) % 1f;
            Color col = Color.HSVToRGB(hue, 1f, 1f);
            SetAll(col, baseIntensity * 2f, 0.8f, 0.9f);

            if (Random.value < 0.12f)
                SpawnBurst(Random.Range(3, 8), Color.HSVToRGB(Random.value, 1f, 1f), 2f, 3f);

            yield return null;
        }
        _eventOverride = false;
    }

    IEnumerator EventLost()
    {
        _eventOverride = true;
        float endTime = s_time + 4f;
        while (s_time < endTime)
        {
            float flicker = Mathf.Abs(Mathf.Sin(s_time * 12f));
            SetAll(ColRed, baseIntensity * flicker, flicker * 0.5f, flicker * 0.4f);
            yield return null;
        }
        _eventOverride = false;
    }

    IEnumerator EventBallDrop()
    {
        _eventOverride = true;
        SetAll(baseColor, baseIntensity * 1.5f, 0.5f, 0.6f);
        SpawnBurst(5, baseColor, 1f, 2f);
        yield return new WaitForSeconds(0.4f);
        _eventOverride = false;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PARTICLE EMITTER
    // ────────────────────────────────────────────────────────

    IEnumerator ParticleEmitter()
    {
        while (true)
        {
            float curIntensity = pointLight ? pointLight.intensity : 0f;
            if (curIntensity > baseIntensity * 0.4f)
            {
                // Ambient sparks off the panel surface
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = transform.position
                        + transform.right * Random.Range(-0.45f, 0.45f)
                        + transform.up * Random.Range(-0.15f, 0.15f)
                        + transform.forward * 0.12f;
                    Vector3 vel = transform.forward * Random.Range(0.05f, 0.2f)
                                + transform.up * Random.Range(0.1f, 0.5f)
                                + new Vector3(Random.Range(-0.1f, 0.1f), 0,
                                              Random.Range(-0.1f, 0.1f));
                    Color col = pointLight ? pointLight.color : baseColor;
                    col.a = 0.9f;
                    SpawnParticle(pos, vel, col,
                        Random.Range(0.2f, 0.7f),
                        Random.Range(0.03f, 0.09f));
                }
            }

            // Beam glow particles rising up the beams
            if (_beamA && _beamMatA != null)
            {
                Color bc = pointLight ? pointLight.color : baseColor;
                float ba = _beamMatA.color.a;
                if (ba > 0.1f)
                {
                    Vector3 beamPos = _beamA.position + Vector3.up * Random.Range(-2f, 2f);
                    SpawnParticle(beamPos,
                        Vector3.up * Random.Range(0.2f, 0.6f),
                        new Color(bc.r, bc.g, bc.b, 0.5f),
                        Random.Range(0.3f, 0.8f),
                        Random.Range(0.02f, 0.06f));
                }
            }

            yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
        }
    }

    void SpawnBurst(int count, Color col, float minSpeed, float maxSpeed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = transform.position
                + transform.right * Random.Range(-0.4f, 0.4f)
                + transform.up * Random.Range(-0.15f, 0.15f);
            Vector3 vel = (transform.forward * Random.Range(0.2f, 0.6f)
                         + new Vector3(Random.Range(-1f, 1f),
                                       Random.Range(0.2f, 1.2f),
                                       Random.Range(-1f, 1f)).normalized)
                         * Random.Range(minSpeed, maxSpeed);
            Color c = col; c.a = Random.Range(0.7f, 1f);
            SpawnParticle(pos, vel, c,
                Random.Range(0.3f, 0.8f),
                Random.Range(0.04f, 0.12f));
        }
    }

    void SpawnParticle(Vector3 pos, Vector3 vel, Color col, float life, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "LP";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(null);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var mat = MakeUnlitMat(col);
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _particles.Add(new Particle
        {
            t = go.transform,
            mr = mr,
            mat = mat,
            vel = vel,
            acc = Vector3.down * 1.5f,
            born = Time.time,
            life = life,
            col = col,
            sz = size,
            rot = Random.Range(0f, 360f),
            rotSpd = Random.Range(-180f, 180f)
        });
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

            p.vel += p.acc * Time.deltaTime;
            p.vel *= (1f - 0.9f * Time.deltaTime);
            p.t.position += p.vel * Time.deltaTime;
            p.rot += p.rotSpd * Time.deltaTime;
            p.t.localRotation = Quaternion.Euler(0f, p.rot, 0f);
            _particles[i] = p;

            p.t.localScale = Vector3.one * p.sz;
            float alpha = p.col.a * Mathf.Pow(1f - prog, 1.3f);
            p.mat.color = new Color(p.col.r, p.col.g, p.col.b, Mathf.Max(alpha, 0f));
        }
    }

    void CleanParticles()
    {
        foreach (var p in _particles) if (p.t) Destroy(p.t.gameObject);
        _particles.Clear();
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region APPLY HELPERS
    // ────────────────────────────────────────────────────────

    void SetAll(Color col, float intensity, float beamAlpha, float haloAlpha)
    {
        // Point light
        if (pointLight)
        {
            pointLight.color = col;
            pointLight.intensity = Mathf.Max(intensity, 0f);
        }

        // Panel surface emission
        if (_mat)
        {
            _mat.EnableKeyword("_EMISSION");
            _mat.SetColor("_EmissionColor", col * (intensity * 0.2f));
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor",
                    new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 1f));
        }

        // Beams
        if (_beamMatA != null)
        {
            Color bc = new Color(col.r, col.g, col.b, Mathf.Clamp01(beamAlpha));
            _beamMatA.color = bc;
            // Beam B slightly offset colour
            Color bc2 = new Color(
                Mathf.Clamp01(col.r * 0.7f + 0.3f),
                Mathf.Clamp01(col.g * 0.7f),
                Mathf.Clamp01(col.b * 1.1f),
                Mathf.Clamp01(beamAlpha * 0.7f));
            if (_beamMatB != null) _beamMatB.color = bc2;
        }

        // Halo disc
        if (_haloMat != null)
        {
            _haloMat.color = new Color(col.r, col.g, col.b,
                Mathf.Clamp01(haloAlpha * 0.6f));
        }
    }

    Material MakeUnlitMat(Color col)
    {
        var mat = new Material(
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard"));
        mat.color = col;
        return mat;
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (!pointLight) return;
        Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f);
        Gizmos.DrawSphere(transform.position, pointLight.range * 0.3f);
    }
}