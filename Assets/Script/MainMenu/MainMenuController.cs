using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

// ============================================================
//  MainMenuController — FULL AAA SELF-BUILDING Edition
//
//  HOW TO USE:
//  1. DELETE your old MainMenuController from the scene.
//  2. Create an empty GameObject, name it "MainMenu".
//  3. Drag this script onto it. That's it. Hit Play.
//
//  It builds the ENTIRE UI from scratch — background, stars,
//  nebulas, title, rings, buttons, particles, flash overlay.
//  Nothing to wire up. Nothing to drag. Just works.
//
//  Only change "gameSceneName" to match your actual scene name
//  in the Inspector (defaults to "LevelOne").
// ============================================================

public class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "LevelOne";

    [Header("Title")]
    public string gameTitle    = "CORE: LAST CHARGE";
    public string gameSubtitle = "SEASON I  ·  SELECT PLAYERS";

    [Header("Colours — tweak to reskin everything")]
    public Color colBgDeep      = new Color(0.02f, 0.03f, 0.10f, 1f);
    public Color colAccentA     = new Color(0.00f, 0.88f, 1.00f, 1f);   // electric cyan
    public Color colAccentB     = new Color(0.55f, 0.10f, 1.00f, 1f);   // deep violet
    public Color colAccentC     = new Color(1.00f, 0.38f, 0.08f, 1f);   // ember orange
    public Color colTitleTop    = new Color(1.00f, 1.00f, 1.00f, 1f);
    public Color colTitleBot    = new Color(0.55f, 0.88f, 1.00f, 1f);
    public Color colButtonFace  = new Color(0.04f, 0.11f, 0.26f, 0.94f);
    public Color colButtonBorder= new Color(0.00f, 0.88f, 1.00f, 0.70f);
    public Color colButtonText  = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    [Header("Audio (optional — leave empty if none)")]
    public AudioClip sfxHover;
    public AudioClip sfxClick;
    public AudioSource musicSource;

    // ── Runtime refs ─────────────────────────────────────────
    private Canvas          _canvas;
    private AudioSource     _sfx;
    private bool            _transitioning;

    private TextMeshProUGUI  _titleTMP;
    private TextMeshProUGUI  _subtitleTMP;
    private Image            _flashOverlay;
    private RectTransform    _titleContainer;
    private CanvasGroup      _titleCG;
    private Image            _scanlines;
    private List<Image>      _glowRings  = new List<Image>();
    private List<(RectTransform rt, CanvasGroup cg)> _btnRoots = new List<(RectTransform, CanvasGroup)>();

    private struct StarData   { public RectTransform rt; public Image img; public float speed; public float phase; public float baseA; }
    private struct NebulaData { public RectTransform rt; public Image img; public Vector2 dir; public float spd; public float phase; public float baseA; }
    private List<StarData>   _stars   = new List<StarData>();
    private List<NebulaData> _nebulas = new List<NebulaData>();

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
    }

    void Start()
    {
        BuildAll();
        StartCoroutine(IntroSequence());
        StartCoroutine(LoopBackground());
        StartCoroutine(LoopTitle());
        StartCoroutine(LoopRings());
        StartCoroutine(TypewriterRoutine());
    }

    void Update() => TickStars();

    // ────────────────────────────────────────────────────────
    #region BUILD
    // ────────────────────────────────────────────────────────

    void BuildAll()
    {
        _canvas = MakeCanvas();
        LayerBG();
        LayerNebulas();
        LayerStars();
        LayerGrid();
        LayerScanlines();
        LayerVignette();
        LayerRings();
        LayerTitle();
        LayerButtons();
        LayerFlash();
        HideForIntro();
    }

    Canvas MakeCanvas()
    {
        var go = new GameObject("MenuCanvas");
        go.transform.SetParent(transform, false);
        var c  = go.AddComponent<Canvas>();
        c.renderMode  = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 99;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode           = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution   = new Vector2(1920, 1080);
        cs.matchWidthOrHeight    = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (!FindAnyObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        return c;
    }

    void LayerBG()
    {
        // Near-black deep space base
        var bg = Img("BG", _canvas.transform, colBgDeep);
        Stretch(bg.rectTransform);

        // Subtle blue centre-glow
        var glow = Img("BGGlow", _canvas.transform, new Color(0.04f, 0.10f, 0.28f, 0.55f));
        Stretch(glow.rectTransform);
    }

    void LayerNebulas()
    {
        var defs = new[]
        {
            (col:new Color(colAccentB.r,colAccentB.g,colAccentB.b,0.13f), w:950f, h:700f, x:-320f, y: 220f, dx: 0.30f, dy: 0.14f, spd: 9f),
            (col:new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.07f), w:800f, h:600f, x: 380f, y:-180f, dx:-0.22f, dy: 0.28f, spd:11f),
            (col:new Color(colAccentC.r,colAccentC.g,colAccentC.b,0.06f), w:600f, h:500f, x:-120f, y:-320f, dx: 0.18f, dy:-0.22f, spd:13f),
            (col:new Color(colAccentB.r,colAccentB.g,colAccentB.b,0.07f), w:500f, h:400f, x: 520f, y: 310f, dx:-0.28f, dy: 0.12f, spd:10f),
        };
        foreach (var d in defs)
        {
            var img = Img("Neb", _canvas.transform, d.col);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = Vector2.one * 0.5f;
            img.rectTransform.sizeDelta = new Vector2(d.w, d.h);
            img.rectTransform.anchoredPosition = new Vector2(d.x, d.y);
            _nebulas.Add(new NebulaData { rt=img.rectTransform, img=img, dir=new Vector2(d.dx,d.dy).normalized, spd=d.spd, phase=Random.value*Mathf.PI*2f, baseA=d.col.a });
        }
    }

    void LayerStars()
    {
        var parent = Rect("Stars", _canvas.transform);
        Stretch(parent);
        for (int i = 0; i < 300; i++)
        {
            float sz  = Random.value < 0.08f ? Random.Range(2.5f,4.5f) : Random.Range(0.7f,2.2f);
            float alp = Random.Range(0.25f,0.95f);
            var img = Img("S", parent, new Color(1f, 1f, Random.Range(0.88f,1f), alp));
            img.rectTransform.sizeDelta = Vector2.one * sz;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(Random.value, Random.value);
            img.rectTransform.anchoredPosition = Vector2.zero;
            _stars.Add(new StarData { rt=img.rectTransform, img=img, speed=Random.Range(0.4f,1.5f), phase=Random.value*Mathf.PI*2f, baseA=alp });
        }
    }

    void LayerGrid()
    {
        var p = Rect("Grid", _canvas.transform);
        Stretch(p);
        Color lc = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.028f);
        for (int i = 0; i < 11; i++)
        {
            var l = Img("H", p, lc);
            l.rectTransform.anchorMin = new Vector2(0f,0f);
            l.rectTransform.anchorMax = new Vector2(1f,0f);
            l.rectTransform.sizeDelta = new Vector2(0f,1f);
            l.rectTransform.anchoredPosition = new Vector2(0f, i * 100f);
            l.raycastTarget = false;
        }
        for (int i = 0; i < 9; i++)
        {
            var l = Img("V", p, lc);
            l.rectTransform.anchorMin = new Vector2(0f,0f);
            l.rectTransform.anchorMax = new Vector2(0f,1f);
            l.rectTransform.sizeDelta = new Vector2(1f,0f);
            l.rectTransform.anchoredPosition = new Vector2(i * 220f, 0f);
            l.raycastTarget = false;
        }
    }

    void LayerScanlines()
    {
        _scanlines = Img("Scan", _canvas.transform, new Color(0f,0f,0f,0.04f));
        Stretch(_scanlines.rectTransform);
        _scanlines.raycastTarget = false;
    }

    void LayerVignette()
    {
        var v = Img("Vign", _canvas.transform, new Color(0f,0f,0f,0.5f));
        Stretch(v.rectTransform);
        v.raycastTarget = false;
    }

    void LayerRings()
    {
        _titleContainer = Rect("TitleArea", _canvas.transform);
        _titleContainer.anchorMin = _titleContainer.anchorMax = _titleContainer.pivot = Vector2.one * 0.5f;
        _titleContainer.sizeDelta = new Vector2(920f, 300f);
        _titleContainer.anchoredPosition = new Vector2(0f, 130f);
        _titleCG = _titleContainer.gameObject.AddComponent<CanvasGroup>();

        float[] sz  = {530f, 430f, 330f};
        float[] fa  = {0.72f, 0.60f, 0.48f};
        Color[] rc  = {
            new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.19f),
            new Color(colAccentB.r,colAccentB.g,colAccentB.b,0.14f),
            new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.10f),
        };
        for (int i = 0; i < 3; i++)
        {
            var r = Img("Ring"+i, _titleContainer, rc[i]);
            r.rectTransform.anchorMin = r.rectTransform.anchorMax = r.rectTransform.pivot = Vector2.one*0.5f;
            r.rectTransform.sizeDelta = Vector2.one * sz[i];
            r.rectTransform.anchoredPosition = Vector2.zero;
            r.type       = Image.Type.Filled;
            r.fillMethod = Image.FillMethod.Radial360;
            r.fillAmount = fa[i];
            r.raycastTarget = false;
            _glowRings.Add(r);
        }
    }

    void LayerTitle()
    {
        // Drop shadow
        var sh = TMP("Shadow", _titleContainer, gameTitle);
        sh.fontSize   = 92f;
        sh.fontStyle  = FontStyles.Bold | FontStyles.UpperCase;
        sh.alignment  = TextAlignmentOptions.Center;
        sh.color      = new Color(0f, 0.45f, 0.85f, 0.32f);
        sh.rectTransform.anchorMin = sh.rectTransform.anchorMax = sh.rectTransform.pivot = Vector2.one*0.5f;
        sh.rectTransform.sizeDelta = new Vector2(920f, 200f);
        sh.rectTransform.anchoredPosition = new Vector2(5f, -7f);
        sh.raycastTarget = false;

        // Main title
        _titleTMP = TMP("Title", _titleContainer, gameTitle);
        _titleTMP.fontSize  = 92f;
        _titleTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        _titleTMP.alignment = TextAlignmentOptions.Center;
        _titleTMP.color     = colTitleTop;
        _titleTMP.enableVertexGradient = true;
        _titleTMP.colorGradient = new VertexGradient(colTitleTop, colTitleTop, colTitleBot, colTitleBot);
        _titleTMP.rectTransform.anchorMin = _titleTMP.rectTransform.anchorMax = _titleTMP.rectTransform.pivot = Vector2.one*0.5f;
        _titleTMP.rectTransform.sizeDelta = new Vector2(920f, 200f);
        _titleTMP.rectTransform.anchoredPosition = Vector2.zero;
        _titleTMP.raycastTarget = false;

        // Accent line
        var line = Img("Line", _titleContainer, new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.55f));
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = line.rectTransform.pivot = Vector2.one*0.5f;
        line.rectTransform.sizeDelta = new Vector2(640f, 1.5f);
        line.rectTransform.anchoredPosition = new Vector2(0f, -98f);
        line.raycastTarget = false;

        // Subtitle
        _subtitleTMP = TMP("Sub", _titleContainer, "");
        _subtitleTMP.fontSize  = 17f;
        _subtitleTMP.alignment = TextAlignmentOptions.Center;
        _subtitleTMP.color     = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.72f);
        _subtitleTMP.characterSpacing = 7f;
        _subtitleTMP.rectTransform.anchorMin = _subtitleTMP.rectTransform.anchorMax = _subtitleTMP.rectTransform.pivot = Vector2.one*0.5f;
        _subtitleTMP.rectTransform.sizeDelta = new Vector2(920f, 40f);
        _subtitleTMP.rectTransform.anchoredPosition = new Vector2(0f, -122f);
        _subtitleTMP.raycastTarget = false;

        // Corner brackets
        MakeBrackets(_titleContainer, 430f, 90f);
    }

    void MakeBrackets(RectTransform p, float halfW, float halfH)
    {
        Color bc = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.45f);
        float bsz = 26f, th = 1.8f;
        var corners = new[]{ new Vector2(-halfW,halfH), new Vector2(halfW,halfH), new Vector2(-halfW,-halfH), new Vector2(halfW,-halfH) };
        float[] hf  = {1f,-1f, 1f,-1f};
        float[] vf  = {1f, 1f,-1f,-1f};
        for (int i = 0; i < 4; i++)
        {
            var h = Img("Bh"+i, p, bc);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = h.rectTransform.pivot = Vector2.one*0.5f;
            h.rectTransform.sizeDelta = new Vector2(bsz, th);
            h.rectTransform.anchoredPosition = corners[i] + new Vector2(hf[i]*(bsz/2f-th/2f), 0f);
            h.raycastTarget = false;
            var v = Img("Bv"+i, p, bc);
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = Vector2.one*0.5f;
            v.rectTransform.sizeDelta = new Vector2(th, bsz);
            v.rectTransform.anchoredPosition = corners[i] + new Vector2(0f, vf[i]*(bsz/2f-th/2f));
            v.raycastTarget = false;
        }
    }

    void LayerButtons()
    {
        var defs = new[]
        {
            (txt:"2  PLAYERS", badge:"DUEL",     count:2),
            (txt:"3  PLAYERS", badge:"SKIRMISH", count:3),
            (txt:"4  PLAYERS", badge:"CHAOS",    count:4),
        };

        var container = Rect("Btns", _canvas.transform);
        container.anchorMin = container.anchorMax = container.pivot = Vector2.one*0.5f;
        container.sizeDelta = new Vector2(500f, 250f);
        container.anchoredPosition = new Vector2(0f, -210f);

        for (int i = 0; i < defs.Length; i++)
        {
            int cap = defs[i].count;
            float y = 88f - i * 88f;

            // Root
            var root = Rect("BRoot"+i, container);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f,0.5f);
            root.sizeDelta = new Vector2(480f, 68f);
            root.anchoredPosition = new Vector2(-80f, y);  // will slide in
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            // Border
            var border = Img("Bo", root, colButtonBorder);
            Stretch(border.rectTransform); border.raycastTarget = false;

            // Face
            var face = Img("Fa", root, colButtonFace);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = new Vector2(2f,2f);
            face.rectTransform.offsetMax = new Vector2(-2f,-2f);
            face.raycastTarget = false;

            // Left accent
            var acc = Img("Ac", root, colAccentA);
            acc.rectTransform.anchorMin = new Vector2(0f,0.5f);
            acc.rectTransform.anchorMax = new Vector2(0f,0.5f);
            acc.rectTransform.pivot     = new Vector2(0f,0.5f);
            acc.rectTransform.sizeDelta = new Vector2(4f,36f);
            acc.rectTransform.anchoredPosition = new Vector2(2f,0f);
            acc.raycastTarget = false;

            // Label
            var lbl = TMP("Lbl", root, defs[i].txt);
            lbl.fontSize  = 26f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color     = colButtonText;
            lbl.characterSpacing = 4f;
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(24f, 0f);
            lbl.rectTransform.offsetMax = new Vector2(-100f, 0f);
            lbl.raycastTarget = false;

            // Badge bg
            var bbg = Img("BBg", root, new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.14f));
            bbg.rectTransform.anchorMin = bbg.rectTransform.anchorMax = new Vector2(1f,0.5f);
            bbg.rectTransform.pivot     = new Vector2(1f,0.5f);
            bbg.rectTransform.sizeDelta = new Vector2(98f,32f);
            bbg.rectTransform.anchoredPosition = new Vector2(-12f,0f);
            bbg.raycastTarget = false;

            // Badge label
            var btxt = TMP("BTxt", bbg.rectTransform, defs[i].badge);
            btxt.fontSize  = 12f;
            btxt.fontStyle = FontStyles.Bold;
            btxt.alignment = TextAlignmentOptions.Center;
            btxt.color     = new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.88f);
            btxt.characterSpacing = 3f;
            btxt.rectTransform.anchorMin = Vector2.zero;
            btxt.rectTransform.anchorMax = Vector2.one;
            btxt.rectTransform.offsetMin = btxt.rectTransform.offsetMax = Vector2.zero;
            btxt.raycastTarget = false;

            // Invisible hit area
            var hit = Img("Hit", root, Color.clear);
            Stretch(hit.rectTransform);
            hit.raycastTarget = true;
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition    = Selectable.Transition.None;
            int c2 = cap;
            btn.onClick.AddListener(() => OnClick(c2, root, face, border));

            // Hover
            var et = root.gameObject.AddComponent<EventTrigger>();
            AddTrigger(et, EventTriggerType.PointerEnter, _ => { PlaySfx(sfxHover, 0.3f); StartCoroutine(HoverAnim(root, face, border, acc, lbl, true)); });
            AddTrigger(et, EventTriggerType.PointerExit,  _ => StartCoroutine(HoverAnim(root, face, border, acc, lbl, false)));

            _btnRoots.Add((root, cg));
        }
    }

    void LayerFlash()
    {
        _flashOverlay = Img("Flash", _canvas.transform, new Color(colAccentA.r,colAccentA.g,colAccentA.b,0f));
        Stretch(_flashOverlay.rectTransform);
        _flashOverlay.raycastTarget = false;
    }

    void HideForIntro()
    {
        if (_titleCG) _titleCG.alpha = 0f;
        _titleContainer.localScale = Vector3.one * 0.85f;
        foreach (var (rt, cg) in _btnRoots) { cg.alpha = 0f; }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region INTRO
    // ────────────────────────────────────────────────────────

    IEnumerator IntroSequence()
    {
        yield return new WaitForSeconds(0.25f);

        // Title fade+scale in
        float t = 0f;
        while (t < 0.85f)
        {
            t += Time.deltaTime;
            float p = OutCubic(t / 0.85f);
            _titleCG.alpha = p;
            _titleContainer.localScale = Vector3.Lerp(Vector3.one*0.85f, Vector3.one, p);
            yield return null;
        }
        _titleCG.alpha = 1f; _titleContainer.localScale = Vector3.one;

        yield return new WaitForSeconds(0.1f);

        // Buttons slide in
        for (int i = 0; i < _btnRoots.Count; i++)
        {
            var (rt, cg) = _btnRoots[i];
            Vector2 target = rt.anchoredPosition + new Vector2(80f,0f);
            StartCoroutine(SlideIn(rt, cg, rt.anchoredPosition, target, 0.45f));
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator SlideIn(RectTransform rt, CanvasGroup cg, Vector2 from, Vector2 to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = OutCubic(Mathf.Clamp01(t/dur));
            cg.alpha = p;
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            yield return null;
        }
        cg.alpha = 1f; rt.anchoredPosition = to;
        cg.interactable = true; cg.blocksRaycasts = true;
    }

    IEnumerator TypewriterRoutine()
    {
        yield return new WaitForSeconds(1.05f);
        _subtitleTMP.text = "";
        foreach (char c in gameSubtitle)
        {
            _subtitleTMP.text += c;
            yield return new WaitForSeconds(0.038f);
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region AMBIENT LOOPS
    // ────────────────────────────────────────────────────────

    IEnumerator LoopBackground()
    {
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _nebulas.Count; i++)
            {
                var n = _nebulas[i];
                n.rt.anchoredPosition += n.dir * n.spd * Time.deltaTime;
                var p = n.rt.anchoredPosition;
                if (Mathf.Abs(p.x) > 680f) n.dir.x *= -1f;
                if (Mathf.Abs(p.y) > 480f) n.dir.y *= -1f;
                _nebulas[i] = n;
                var c = n.img.color;
                c.a = n.baseA * (0.55f + 0.45f * Mathf.Sin(t*0.38f + n.phase));
                n.img.color = c;
            }
            if (_scanlines)
            {
                var sc = _scanlines.color;
                sc.a = 0.022f + 0.014f * Mathf.Sin(t*6.8f);
                _scanlines.color = sc;
            }
            yield return null;
        }
    }

    void TickStars()
    {
        float t = Time.time;
        foreach (var s in _stars)
        {
            if (!s.img) continue;
            float a = s.baseA * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t*s.speed + s.phase)));
            var c = s.img.color; c.a = a; s.img.color = c;
        }
    }

    IEnumerator LoopTitle()
    {
        while (true)
        {
            float t = Time.time;
            if (_titleTMP)
            {
                float p = (Mathf.Sin(t*0.65f)+1f)*0.5f;
                Color top = Color.Lerp(colTitleTop, new Color(0.82f,0.96f,1f), p);
                Color bot = Color.Lerp(colTitleBot, new Color(0.38f,0.70f,1f), p);
                _titleTMP.colorGradient = new VertexGradient(top,top,bot,bot);
            }
            if (_titleContainer)
            {
                float sc = 1f + 0.007f * Mathf.Sin(t*1.05f);
                _titleContainer.localScale = Vector3.one * sc;
            }
            yield return null;
        }
    }

    IEnumerator LoopRings()
    {
        float[] spd = {16f,-11f,20f};
        float[] ph  = {0f,1.1f,2.3f};
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < _glowRings.Count; i++)
            {
                var r = _glowRings[i];
                if (!r) continue;
                r.rectTransform.Rotate(0f,0f,spd[i]*Time.deltaTime);
                float sc = 1f + 0.028f*Mathf.Sin(t*1.35f+ph[i]);
                r.rectTransform.localScale = Vector3.one*sc;
                var c = r.color;
                float baseA = i==0?0.19f:i==1?0.14f:0.10f;
                c.a = baseA*(0.55f+0.45f*Mathf.Sin(t*1.75f+ph[i]));
                r.color = c;
            }
            yield return null;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region HOVER
    // ────────────────────────────────────────────────────────

    IEnumerator HoverAnim(RectTransform root, Image face, Image border, Image acc, TextMeshProUGUI lbl, bool enter)
    {
        float dur = 0.11f, el = 0f;
        float   ts = enter ? 1.04f : 1f;
        Color   tf = enter ? new Color(colButtonFace.r+0.07f, colButtonFace.g+0.09f, colButtonFace.b+0.14f, colButtonFace.a) : colButtonFace;
        Color   tb = enter ? new Color(colAccentA.r,colAccentA.g,colAccentA.b,1f)  : colButtonBorder;
        Color   ta = enter ? colAccentC : colAccentA;
        Color   tl = enter ? colAccentA : colButtonText;
        Vector3 ss = root.localScale; Color sf = face.color; Color sb = border.color; Color sa = acc.color; Color sl = lbl.color;
        while (el < dur)
        {
            el += Time.deltaTime;
            float p = OutCubic(Mathf.Clamp01(el/dur));
            root.localScale = Vector3.Lerp(ss, Vector3.one*ts, p);
            face.color   = Color.Lerp(sf, tf, p);
            border.color = Color.Lerp(sb, tb, p);
            acc.color    = Color.Lerp(sa, ta, p);
            lbl.color    = Color.Lerp(sl, tl, p);
            yield return null;
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region CLICK / LAUNCH
    // ────────────────────────────────────────────────────────

    void OnClick(int count, RectTransform btnRt, Image face, Image border)
    {
        if (_transitioning) return;
        _transitioning = true;
        PlayerPrefs.SetInt("PlayerCount", count);
        PlayerPrefs.Save();
        StartCoroutine(LaunchSeq(btnRt));
    }

    IEnumerator LaunchSeq(RectTransform btnRt)
    {
        PlaySfx(sfxClick);
        StartCoroutine(BurstAt(btnRt.position));
        yield return StartCoroutine(Punch(btnRt));
        yield return StartCoroutine(Flash());
        if (musicSource) StartCoroutine(FadeMusic());
        yield return StartCoroutine(FadeUI(0.45f));
        yield return new WaitForSeconds(0.1f);
        SceneManager.LoadScene(gameSceneName);
    }

    IEnumerator Punch(RectTransform rt)
    {
        float t=0f; while(t<0.07f){t+=Time.deltaTime; rt.localScale=Vector3.Lerp(Vector3.one,Vector3.one*0.86f,t/0.07f); yield return null;}
        t=0f; while(t<0.16f){t+=Time.deltaTime; rt.localScale=Vector3.one*(1f+0.05f*Mathf.Sin((t/0.16f)*Mathf.PI)); yield return null;}
        rt.localScale = Vector3.one;
    }

    IEnumerator Flash()
    {
        if (!_flashOverlay) yield break;
        Color c = new Color(colAccentA.r,colAccentA.g,colAccentA.b,0.38f); _flashOverlay.color=c;
        float t=0f; while(t<0.28f){t+=Time.deltaTime; c.a=Mathf.Lerp(0.38f,0f,t/0.28f); _flashOverlay.color=c; yield return null;}
        c.a=0f; _flashOverlay.color=c;
    }

    IEnumerator FadeUI(float dur)
    {
        var cgs = _canvas.GetComponentsInChildren<CanvasGroup>();
        float t=0f; float[] s=new float[cgs.Length];
        for(int i=0;i<cgs.Length;i++) s[i]=cgs[i].alpha;
        while(t<dur){t+=Time.deltaTime; float p=t/dur; for(int i=0;i<cgs.Length;i++) cgs[i].alpha=Mathf.Lerp(s[i],0f,p); yield return null;}
    }

    IEnumerator FadeMusic()
    {
        if (!musicSource) yield break;
        float v=musicSource.volume, t=0f;
        while(t<1f){t+=Time.deltaTime; musicSource.volume=Mathf.Lerp(v,0f,t); yield return null;}
        musicSource.Stop(); musicSource.volume=v;
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region PROCEDURAL BURST PARTICLES
    // ────────────────────────────────────────────────────────

    IEnumerator BurstAt(Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_canvas.transform,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            _canvas.worldCamera, out Vector2 lp);

        int count = 44;
        Color[] cols = {colAccentA, colAccentB, colAccentC, Color.white};
        var parts = new List<(RectTransform rt, Image img, Vector2 vel, float life, Color col)>();

        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI*2f);
            float spd = Random.Range(130f, 520f);
            float sz  = Random.Range(3f, 11f);
            Color col = cols[Random.Range(0, cols.Length)];
            var img   = Img("P", _canvas.transform, col);
            img.rectTransform.sizeDelta = Vector2.one*sz;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = Vector2.one*0.5f;
            img.rectTransform.anchoredPosition = lp;
            img.raycastTarget = false;
            parts.Add((img.rectTransform, img, new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*spd, Random.Range(0.3f,0.72f), col));
        }

        float elapsed = 0f;
        while (elapsed < 0.75f)
        {
            elapsed += Time.deltaTime;
            for (int i = parts.Count-1; i >= 0; i--)
            {
                var (rt, img, vel, life, col) = parts[i];
                if (!rt) { parts.RemoveAt(i); continue; }
                float prog = elapsed/life;
                if (prog >= 1f) { Destroy(rt.gameObject); parts.RemoveAt(i); continue; }
                rt.anchoredPosition += vel * Time.deltaTime;
                var newVel = vel * (1f - 5.5f*Time.deltaTime);
                parts[i] = (rt, img, newVel, life, col);
                float a = Mathf.Lerp(1f, 0f, prog*prog);
                img.color = new Color(col.r,col.g,col.b,a);
                rt.localScale = Vector3.one*Mathf.Lerp(1f,0f,prog);
            }
            yield return null;
        }
        foreach (var (rt,_,_,_,_) in parts) if (rt) Destroy(rt.gameObject);
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region UTILITY
    // ────────────────────────────────────────────────────────

    Image Img(string n, Transform p, Color c)
    {
        var go  = new GameObject(n); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    RectTransform Rect(string n, Transform p)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        return go.AddComponent<RectTransform>();
    }

    TextMeshProUGUI TMP(string n, Transform p, string text)
    {
        var go  = new GameObject(n); go.transform.SetParent(p, false);
        var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = text; return tmp;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin  = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot      = Vector2.one*0.5f;
    }

    void AddTrigger(EventTrigger et, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(cb);
        et.triggers.Add(entry);
    }

    void PlaySfx(AudioClip clip, float vol = 1f)
    { if (clip && _sfx) _sfx.PlayOneShot(clip, vol); }

    static float OutCubic(float t) => 1f - Mathf.Pow(1f-t, 3f);

    #endregion
}