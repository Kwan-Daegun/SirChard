using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "LevelOne";

    [Header("Title")]
    public string gameTitle    = "CORE: LAST CHARGE";
    public string gameSubtitle = "SEASON I";

    [Header("Font")]
    public TMP_FontAsset audiowideFont;

    // ✨ NEW: Background Runners - drag your prefabs here
    [Header("Background Runners")]
    [Tooltip("Leave empty to use built in placeholder runners")]
    public Transform player1;
    public Transform player2;
    public Transform player3;
    public Transform player4;
    public Transform energyBall;
    public float runSpeed = 14f;
    public float runYPosition = 0f;

    [Header("Colours")]
    public Color colBg          = new Color(0.05f, 0.06f, 0.12f, 1f);
    public Color colAccentA     = new Color(0.00f, 0.88f, 1.00f, 1f);
    public Color colAccentB     = new Color(0.55f, 0.10f, 1.00f, 1f);
    public Color colAccentC     = new Color(1.00f, 0.38f, 0.08f, 1f);
    public Color colTitleTop    = new Color(1.00f, 1.00f, 1.00f, 1f);
    public Color colTitleBot    = new Color(0.55f, 0.88f, 1.00f, 1f);
    public Color colButtonFace  = new Color(0.04f, 0.11f, 0.26f, 0.95f);
    public Color colButtonBorder= new Color(0.00f, 0.88f, 1.00f, 0.70f);
    public Color colButtonText  = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    [Header("Audio")]
    public AudioClip sfxHover;
    public AudioClip sfxClick;
    public AudioSource musicSource;

    static readonly Color[] PlayerColors = {
        new Color(1.00f, 0.22f, 0.22f, 1f),
        new Color(0.35f, 0.65f, 1.00f, 1f),
        new Color(0.15f, 0.90f, 0.75f, 1f),
        new Color(1.00f, 0.88f, 0.15f, 1f),
    };

    private Canvas          _canvas;
    private AudioSource     _sfx;
    private bool            _transitioning;

    // Panels
    private CanvasGroup     _mainPanel;
    private CanvasGroup     _playerSelectPanel;
    private CanvasGroup     _howToPlayPanel;
    private CanvasGroup     _creditsPanel;

    // Title refs
    private TextMeshProUGUI _titleTMP;
    private TextMeshProUGUI _subtitleTMP;
    private Image           _flashOverlay;
    private RectTransform   _titleContainer;
    private CanvasGroup     _titleCG;

    // BG player data
    private Transform[]     _bgPlayers = new Transform[4];
    private int             _ballCarrier = 0;
    private bool            _runningRight = true;

    void Awake()
    {
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
    }

    void Start()
    {
        BuildAll();
        StartCoroutine(IntroSequence());
        StartCoroutine(LoopBGPlayers());
    }

    #region BUILD
    void BuildAll()
    {
        _canvas = MakeCanvas();

        //var bg = Img("BG", _canvas.transform, colBg);
        //Stretch(bg.rectTransform);

        // ✅ Auto detect if you assigned your players or if we should use placeholders
        if (player1 == null || player2 == null || player3 == null || player4 == null)
        {
            BuildPlaceholderBGPlayers();
        }
        else
        {
            _bgPlayers[0] = player1;
            _bgPlayers[1] = player2;
            _bgPlayers[2] = player3;
            _bgPlayers[3] = player4;
        }

        //var grad = Img("BottomGrad", _canvas.transform, new Color(0f, 0f, 0f, 0.5f));
        //grad.rectTransform.anchorMin = Vector2.zero;
        //grad.rectTransform.anchorMax = new Vector2(1f, 0.25f);
        //grad.rectTransform.offsetMin = grad.rectTransform.offsetMax = Vector2.zero;
        //grad.raycastTarget = false;

        BuildTitle();

        _mainPanel         = BuildMainPanel();
        _playerSelectPanel = BuildPlayerSelectPanel();
        _howToPlayPanel    = BuildHowToPlayPanel();
        _creditsPanel      = BuildCreditsPanel();

        _flashOverlay = Img("Flash", _canvas.transform, new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0f));
        Stretch(_flashOverlay.rectTransform);
        _flashOverlay.raycastTarget = false;

        SetPanelVisible(_mainPanel,         true,  instant: true);
        SetPanelVisible(_playerSelectPanel, false, instant: true);
        SetPanelVisible(_howToPlayPanel,    false, instant: true);
        SetPanelVisible(_creditsPanel,      false, instant: true);
    }

    Canvas MakeCanvas()
    {
        var go = new GameObject("MenuCanvas");
        go.transform.SetParent(transform, false);
        var c  = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 99;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (!FindAnyObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
        return c;
    }

    void BuildTitle()
    {
        _titleContainer = MakeRect("TitleArea", _canvas.transform);
        _titleContainer.anchorMin = new Vector2(0f, 1f);
        _titleContainer.anchorMax = new Vector2(1f, 1f);
        _titleContainer.pivot     = new Vector2(0.5f, 1f);
        _titleContainer.sizeDelta = new Vector2(0f, 180f);
        _titleContainer.anchoredPosition = Vector2.zero;
        _titleCG = _titleContainer.gameObject.AddComponent<CanvasGroup>();

        _titleTMP = MakeTMP("Title", _titleContainer, gameTitle);
        ApplyFont(_titleTMP);
        _titleTMP.fontSize   = 82f;
        _titleTMP.fontStyle  = FontStyles.Bold | FontStyles.UpperCase;
        _titleTMP.alignment  = TextAlignmentOptions.Center;
        _titleTMP.color      = colTitleTop;
        _titleTMP.enableVertexGradient = true;
        _titleTMP.colorGradient = new VertexGradient(colTitleTop, colTitleTop, colTitleBot, colTitleBot);
        _titleTMP.rectTransform.anchorMin = Vector2.zero;
        _titleTMP.rectTransform.anchorMax = Vector2.one;
        _titleTMP.rectTransform.offsetMin = new Vector2(0f, 40f);
        _titleTMP.rectTransform.offsetMax = Vector2.zero;
        _titleTMP.raycastTarget = false;

        var line = Img("Line", _titleContainer, new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.6f));
        line.rectTransform.anchorMin = new Vector2(0.2f, 0f);
        line.rectTransform.anchorMax = new Vector2(0.8f, 0f);
        line.rectTransform.sizeDelta = new Vector2(0f, 2f);
        line.rectTransform.anchoredPosition = new Vector2(0f, 28f);
        line.raycastTarget = false;

        _subtitleTMP = MakeTMP("Sub", _titleContainer, "");
        ApplyFont(_subtitleTMP);
        _subtitleTMP.fontSize  = 15f;
        _subtitleTMP.alignment = TextAlignmentOptions.Center;
        _subtitleTMP.color     = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.75f);
        _subtitleTMP.characterSpacing = 8f;
        _subtitleTMP.rectTransform.anchorMin = new Vector2(0f, 0f);
        _subtitleTMP.rectTransform.anchorMax = new Vector2(1f, 0f);
        _subtitleTMP.rectTransform.sizeDelta = new Vector2(0f, 30f);
        _subtitleTMP.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        _subtitleTMP.raycastTarget = false;
    }

    CanvasGroup BuildMainPanel()
    {
        var panel = MakePanel("MainPanel");

        float startY = -80f;
        float spacing = 80f;

        MakeButton(panel, "START",      startY + spacing * 0,  colAccentA, () => ShowPlayerSelect());
        MakeButton(panel, "HOW TO PLAY",startY + spacing * -1, colAccentA, () => ShowPanel(_howToPlayPanel));
        MakeButton(panel, "CREDITS",    startY + spacing * -2, colAccentA, () => ShowPanel(_creditsPanel));
        MakeButton(panel, "QUIT",       startY + spacing * -3, new Color(1f, 0.35f, 0.35f, 0.8f), QuitGame);

        return panel.GetComponent<CanvasGroup>();
    }

    CanvasGroup BuildPlayerSelectPanel()
    {
        var panel = MakePanel("PlayerSelectPanel");

        var hdr = MakeTMP("Hdr", panel.transform, "SELECT PLAYERS");
        ApplyFont(hdr);
        hdr.fontSize  = 28f;
        hdr.fontStyle = FontStyles.Bold;
        hdr.alignment = TextAlignmentOptions.Center;
        hdr.color     = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.9f);
        hdr.rectTransform.anchorMin = hdr.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hdr.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
        hdr.rectTransform.sizeDelta = new Vector2(500f, 50f);
        hdr.rectTransform.anchoredPosition = new Vector2(0f, 80f);
        hdr.raycastTarget = false;

        float startY  = -20f;
        float spacing = 80f;

        MakeButton(panel, "2  PLAYERS", startY + spacing * 1,  colAccentA, () => LaunchGame(2));
        MakeButton(panel, "3  PLAYERS", startY + spacing * 0,  colAccentA, () => LaunchGame(3));
        MakeButton(panel, "4  PLAYERS", startY + spacing * -1, colAccentA, () => LaunchGame(4));

        MakeButton(panel, "← BACK", startY + spacing * -2.5f, new Color(0.5f, 0.5f, 0.5f, 0.6f), () => ShowPanel(_mainPanel));

        return panel.GetComponent<CanvasGroup>();
    }

    CanvasGroup BuildHowToPlayPanel()
    {
        var panel = MakePanel("HowToPlayPanel");

        var title = MakeTMP("Title", panel.transform, "HOW TO PLAY");
        ApplyFont(title);
        title.fontSize  = 32f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color     = colAccentA;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(700f, 50f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 160f);
        title.raycastTarget = false;

        string[] lines = {
            "GRAB THE ENERGY BALL AND HOLD IT TO SCORE",
            "TACKLE OTHER PLAYERS TO STEAL THE BALL",
            "AVOID THE TRAPS ON THE ARENA FLOOR",
            "LAST PLAYER STANDING WINS",
        };

        Color[] lineColors = {
            new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.9f),
            new Color(1f, 0.88f, 0.15f, 0.9f),
            new Color(1f, 0.35f, 0.35f, 0.9f),
            new Color(0.15f, 0.90f, 0.75f, 0.9f),
        };

        for (int i = 0; i < lines.Length; i++)
        {
            var line = MakeTMP("Line"+i, panel.transform, lines[i]);
            ApplyFont(line);
            line.fontSize  = 18f;
            line.alignment = TextAlignmentOptions.Center;
            line.color     = lineColors[i];
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(800f, 40f);
            line.rectTransform.anchoredPosition = new Vector2(0f, 80f - i * 55f);
            line.raycastTarget = false;
        }

        MakeButton(panel, "← BACK", -220f, new Color(0.5f, 0.5f, 0.5f, 0.6f), () => ShowPanel(_mainPanel));

        return panel.GetComponent<CanvasGroup>();
    }

    CanvasGroup BuildCreditsPanel()
    {
        var panel = MakePanel("CreditsPanel");

        var title = MakeTMP("Title", panel.transform, "CREDITS");
        ApplyFont(title);
        title.fontSize  = 32f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color     = colAccentA;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(600f, 50f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 160f);
        title.raycastTarget = false;

        string[] credits = {
            "GAME DESIGN",
            "YOUR NAME HERE",
            "",
            "PROGRAMMING",
            "YOUR NAME HERE",
            "",
            "ART & VISUALS",
            "YOUR NAME HERE",
        };

        for (int i = 0; i < credits.Length; i++)
        {
            bool isHeader = (i % 3 == 0 && credits[i] != "");
            var line = MakeTMP("Credit"+i, panel.transform, credits[i]);
            ApplyFont(line);
            line.fontSize  = isHeader ? 13f : 20f;
            line.fontStyle = isHeader ? FontStyles.Normal : FontStyles.Bold;
            line.alignment = TextAlignmentOptions.Center;
            line.color     = isHeader
                ? new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.6f)
                : new Color(1f, 1f, 1f, 0.9f);
            line.characterSpacing = isHeader ? 5f : 0f;
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(600f, 35f);
            line.rectTransform.anchoredPosition = new Vector2(0f, 80f - i * 38f);
            line.raycastTarget = false;
        }

        MakeButton(panel, "← BACK", -220f, new Color(0.5f, 0.5f, 0.5f, 0.6f), () => ShowPanel(_mainPanel));

        return panel.GetComponent<CanvasGroup>();
    }

    GameObject MakePanel(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 600f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        return go;
    }

    void MakeButton(GameObject panel, string label, float yPos, Color accentColor, System.Action onClick)
    {
        var root = new GameObject("Btn_" + label);
        root.transform.SetParent(panel.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 62f);
        rt.anchoredPosition = new Vector2(0f, yPos);

        var border = Img("Border", root.transform, accentColor);
        Stretch(border.rectTransform);
        border.raycastTarget = false;

        var face = Img("Face", root.transform, colButtonFace);
        face.rectTransform.anchorMin = Vector2.zero;
        face.rectTransform.anchorMax = Vector2.one;
        face.rectTransform.offsetMin = new Vector2(2f, 2f);
        face.rectTransform.offsetMax = new Vector2(-2f, -2f);
        face.raycastTarget = false;

        var acc = Img("Acc", root.transform, accentColor);
        acc.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        acc.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        acc.rectTransform.pivot     = new Vector2(0f, 0.5f);
        acc.rectTransform.sizeDelta = new Vector2(4f, 32f);
        acc.rectTransform.anchoredPosition = new Vector2(2f, 0f);
        acc.raycastTarget = false;

        var lbl = MakeTMP("Label", root.transform, label);
        ApplyFont(lbl);
        lbl.fontSize  = 22f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.color     = colButtonText;
        lbl.characterSpacing = 3f;
        lbl.rectTransform.anchorMin = Vector2.zero;
        lbl.rectTransform.anchorMax = Vector2.one;
        lbl.rectTransform.offsetMin = new Vector2(20f, 0f);
        lbl.rectTransform.offsetMax = new Vector2(-20f, 0f);
        lbl.raycastTarget = false;

        var hit = Img("Hit", root.transform, Color.clear);
        Stretch(hit.rectTransform);
        hit.raycastTarget = true;

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition    = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            PlaySfx(sfxClick);
            onClick?.Invoke();
        });

        var et = root.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerEnter, _ =>
        {
            PlaySfx(sfxHover, 0.3f);
            StartCoroutine(BtnHover(rt, face, border, lbl, accentColor, true));
        });
        AddTrigger(et, EventTriggerType.PointerExit, _ =>
            StartCoroutine(BtnHover(rt, face, border, lbl, accentColor, false)));
    }

    // Built in placeholder runners
    void BuildPlaceholderBGPlayers()
    {
        var parent = MakeRect("BGLayer", _canvas.transform);
        Stretch(parent);

        for (int i = 0; i < 4; i++)
        {
            var root = MakeRect("BGP"+i, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 0.5f);
            root.anchoredPosition = new Vector2(-300f - i * 150f, -200f);
            root.sizeDelta = Vector2.zero;

            Color col = PlayerColors[i];

            var body = Img("Body", root, new Color(col.r, col.g, col.b, 0.55f));
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            body.rectTransform.pivot     = new Vector2(0.5f, 0f);
            body.rectTransform.sizeDelta = new Vector2(44f, 68f);
            body.rectTransform.anchoredPosition = Vector2.zero;

            var head = Img("Head", root, new Color(col.r, col.g, col.b, 0.55f));
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            head.rectTransform.pivot     = new Vector2(0.5f, 0f);
            head.rectTransform.sizeDelta = new Vector2(34f, 34f);
            head.rectTransform.anchoredPosition = new Vector2(0f, 72f);

            _bgPlayers[i] = root.transform;
        }
    }

    #endregion

    #region BG PLAYER LOOP
    IEnumerator LoopBGPlayers()
{
    // ✅ THESE VALUES ARE PERFECT FOR YOUR CAMERA
    float screenLeftEdge  = -22f;
    float screenRightEdge =  22f;
    float bobSpeed        = 9f;

    ResetPositions(_runningRight, screenLeftEdge, screenRightEdge, _ballCarrier);

    while (true)
    {
        float dir = _runningRight ? 1f : -1f;
        bool anyOnScreen = false;

        for (int i = 0; i < 4; i++)
        {
            if (_bgPlayers[i] == null) continue;

            float newX = _bgPlayers[i].position.x + dir * runSpeed * Time.deltaTime;
            float bob  = Mathf.Sin(Time.time * bobSpeed + i * 1.4f) * 0.25f;

            _bgPlayers[i].position = new Vector3(newX, runYPosition + bob, 0);

            // Players automatically face direction
            _bgPlayers[i].localScale = new Vector3(_runningRight ? 1f : -1f, 1f, 1f);

            // Check if any player is still on screen
            if (_runningRight  && newX < screenRightEdge) anyOnScreen = true;
            if (!_runningRight && newX > screenLeftEdge)  anyOnScreen = true;

            // Ball follows carrier
            if (i == _ballCarrier && energyBall != null)
            {
                energyBall.position = _bgPlayers[i].position + new Vector3(0, 2.8f, 0);
                energyBall.position += new Vector3(0, Mathf.Sin(Time.time * 6f) * 0.4f, 0);
            }
        }

        // All players off screen: flip direction
        if (!anyOnScreen)
        {
            _runningRight  = !_runningRight;
            _ballCarrier  = (_ballCarrier + 1) % 4;
            ResetPositions(_runningRight, screenLeftEdge, screenRightEdge, _ballCarrier);

            yield return new WaitForSeconds(0.6f);
        }

        yield return null;
    }
}

void ResetPositions(bool goingRight, float leftEdge, float rightEdge, int ballHolder)
{
    for (int i = 0; i < 4; i++)
    {
        if (_bgPlayers[i] == null) continue;

        // Ball holder runs out front, others follow behind
        float leaderOffset = i == ballHolder ? 0f : 1f + (i < ballHolder ? i : i - 1) * 1f;
        float stagger      = leaderOffset * -3.5f;

        float startX = goingRight
            ? leftEdge + stagger
            : rightEdge - stagger;

        // ✅ Place them on your grid level not on the floor
        _bgPlayers[i].position = new Vector3(startX, 0.1f, 0);
    }
}
    #endregion

    #region PANEL NAVIGATION
    void ShowPlayerSelect()
    {
        StartCoroutine(SwitchPanel(_mainPanel, _playerSelectPanel));
    }

    void ShowPanel(CanvasGroup target)
    {
        CanvasGroup current = null;
        if (_mainPanel.alpha         > 0.5f) current = _mainPanel;
        if (_playerSelectPanel.alpha > 0.5f) current = _playerSelectPanel;
        if (_howToPlayPanel.alpha    > 0.5f) current = _howToPlayPanel;
        if (_creditsPanel.alpha      > 0.5f) current = _creditsPanel;

        if (current != null && current != target)
            StartCoroutine(SwitchPanel(current, target));
        else
            StartCoroutine(FadeInPanel(target));
    }

    IEnumerator SwitchPanel(CanvasGroup from, CanvasGroup to)
    {
        yield return StartCoroutine(FadeOutPanel(from));
        yield return StartCoroutine(FadeInPanel(to));
    }

    IEnumerator FadeInPanel(CanvasGroup cg)
    {
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.25f);
            yield return null;
        }
        cg.alpha          = 1f;
        cg.interactable   = true;
        cg.blocksRaycasts = true;
    }

    IEnumerator FadeOutPanel(CanvasGroup cg)
    {
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(1f - t / 0.2f);
            yield return null;
        }
        cg.alpha = 0f;
    }

    void SetPanelVisible(CanvasGroup cg, bool visible, bool instant = false)
    {
        cg.alpha          = visible ? 1f : 0f;
        cg.interactable   = visible;
        cg.blocksRaycasts = visible;
    }

    #endregion

    #region GAME ACTIONS
    void LaunchGame(int playerCount)
    {
        if (_transitioning) return;
        _transitioning = true;
        PlayerPrefs.SetInt("PlayerCount", playerCount);
        PlayerPrefs.Save();
        StartCoroutine(LaunchSeq());
    }

    IEnumerator LaunchSeq()
    {
        // ✨ Bonus: Make everyone sprint when you click play
        runSpeed *= 5f;

        Color c = new Color(colAccentA.r, colAccentA.g, colAccentA.b, 0.5f);
        _flashOverlay.color = c;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0.5f, 1f, t / 0.4f);
            _flashOverlay.color = c;
            yield return null;
        }
        if (musicSource) StartCoroutine(FadeMusic());
        yield return new WaitForSeconds(0.15f);
        SceneManager.LoadScene(gameSceneName);
    }

    void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    IEnumerator FadeMusic()
    {
        if (!musicSource) yield break;
        float v = musicSource.volume, t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(v, 0f, t);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = v;
    }

    #endregion

    #region INTRO
    IEnumerator IntroSequence()
    {
        if (_titleCG) _titleCG.alpha = 0f;

        yield return new WaitForSeconds(0.3f);

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            if (_titleCG) _titleCG.alpha = t / 0.6f;
            yield return null;
        }
        if (_titleCG) _titleCG.alpha = 1f;

        _subtitleTMP.text = "";
        foreach (char c in gameSubtitle)
        {
            _subtitleTMP.text += c;
            yield return new WaitForSeconds(0.05f);
        }
    }

    #endregion

    #region BUTTON HOVER
    IEnumerator BtnHover(RectTransform rt, Image face, Image border,
        TextMeshProUGUI lbl, Color accentColor, bool enter)
    {
        float dur = 0.1f, el = 0f;
        float   targetScale = enter ? 1.04f : 1f;
        Color   targetFace  = enter
            ? new Color(colButtonFace.r + 0.08f, colButtonFace.g + 0.10f,
                        colButtonFace.b + 0.16f, colButtonFace.a)
            : colButtonFace;
        Color   targetBorder = enter
            ? new Color(accentColor.r, accentColor.g, accentColor.b, 1f)
            : new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
        Color   targetLbl   = enter ? accentColor : colButtonText;

        Vector3 startScale = rt.localScale;
        Color   startFace  = face.color;
        Color   startBord  = border.color;
        Color   startLbl   = lbl.color;

        while (el < dur)
        {
            el += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(el / dur), 3f);
            rt.localScale  = Vector3.Lerp(startScale, Vector3.one * targetScale, p);
            face.color     = Color.Lerp(startFace,  targetFace,   p);
            border.color   = Color.Lerp(startBord,  targetBorder, p);
            lbl.color      = Color.Lerp(startLbl,   targetLbl,    p);
            yield return null;
        }
    }

    #endregion

    #region UTILITY
    void ApplyFont(TextMeshProUGUI tmp)
    {
        if (audiowideFont != null) tmp.font = audiowideFont;
    }

    Image Img(string n, Transform p, Color c)
    {
        var go  = new GameObject(n);
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    RectTransform MakeRect(string n, Transform p)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        return go.AddComponent<RectTransform>();
    }

    TextMeshProUGUI MakeTMP(string n, Transform p, string text)
    {
        var go  = new GameObject(n);
        go.transform.SetParent(p, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        return tmp;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
        rt.pivot      = Vector2.one * 0.5f;
    }

    void AddTrigger(EventTrigger et, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(cb);
        et.triggers.Add(entry);
    }

    void PlaySfx(AudioClip clip, float vol = 1f)
    {
        if (clip && _sfx) _sfx.PlayOneShot(clip, vol);
    }

    #endregion
}