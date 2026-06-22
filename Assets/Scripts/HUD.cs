using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// Neo-brutalist HUD built with Unity's uGUI components (Canvas, Image, Text,
// Button) and a CanvasScaler so text stays big and crisp at any resolution.
// Style: flat bright panels, thick black borders, hard offset shadows.
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    private Font font;
    private GameManager gm;
    private DeploymentController deployment;

    // Dynamic references
    private Text statsText, bannerSub, endTitle, endScore, comboText, cityText, summaryText;
    private GameObject bannerGO, legendGO, controlsGO, endGO, summaryGO, shopGO, shopButtonGO, startButtonGO;
    private Text shopButtonText;
    private Image[] shopFills = new Image[3];
    private Text[] shopCostTexts = new Text[3];
    private bool shopOpen;
    private float infoTimer;     // tempo restante de exibição do tutorial (botão "i")
    // Lido pelo DeploymentController: com a loja aberta, não posiciona unidades.
    public static bool ShopOpen { get; private set; }
    private Image endPanelImg, flashImg, cityFillImg;
    private RectTransform comboRT, cityFillRT, cityPanelRT;
    private float lastCityHealth01 = 1f;
    private float cityBarTarget = 1f;            // last health value we started a bar tween toward
    const string CityBarTweenId = "cityBar";

    private float displayedScore;
    private int lastCombo = -1;
    private GameManager.GameState lastState = (GameManager.GameState)(-1);

    // Lets any system trigger a full-screen colour flash (life lost, victory…).
    public static void Flash(Color c)
    {
        if (Instance != null && Instance.flashImg != null) Instance.flashImg.color = c;
    }

    const float SHADOW = 12f, BORDER = 6f;
    static readonly Color Black  = Color.black;
    static readonly Color White  = Color.white;
    static readonly Color Yellow = new Color(1f, 0.85f, 0.1f);
    static readonly Color Cream  = new Color(0.98f, 0.96f, 0.88f);
    // City-health bar gradient: full = green, half = yellow, critical = red
    static readonly Color CityFull = new Color(0.25f, 0.85f, 0.40f);
    static readonly Color CityMid  = new Color(0.95f, 0.80f, 0.20f);
    static readonly Color CityLow  = new Color(0.90f, 0.20f, 0.20f);

    void Start()
    {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deployment = FindAnyObjectByType<DeploymentController>();
        BuildEventSystem();
        BuildUI();
    }

    void Update()
    {
        gm = GameManager.Instance;
        if (gm == null) return;

        // Animated score count-up (min 60/s, faster the bigger the gap)
        displayedScore = Mathf.MoveTowards(displayedScore, gm.score,
            Mathf.Max(60f, Mathf.Abs(gm.score - displayedScore) * 6f) * Time.unscaledDeltaTime);
        int shown = Mathf.RoundToInt(displayedScore);
        string lixos = (gm.CurrentState == GameManager.GameState.Action && TrashSpawner.Instance != null)
            ? $"\nLIXOS: {TrashSpawner.Instance.TrashRemaining}" : "";
        statsText.text = $"PONTOS: {shown}\nONDA: {gm.GetCurrentWave()}/{gm.totalWaves}\nMOEDAS: {gm.coins}{lixos}";

        // City-health meter (lose condition): full bar = healthy, empty = destroyed.
        // The fill width is tweened with DOTween so health changes glide instead of
        // snapping; colour + % text follow the *animated* value for a smooth read.
        float h01 = gm.CityHealth01;
        if (Mathf.Abs(h01 - cityBarTarget) > 0.0001f)
        {
            cityBarTarget = h01;
            DOTween.Kill(CityBarTweenId);
            DOTween.To(() => cityFillRT.anchorMax.x,
                       x => cityFillRT.anchorMax = new Vector2(x, 1f),
                       h01, 0.45f)
                   .SetEase(Ease.OutCubic).SetId(CityBarTweenId).SetUpdate(true);

            // Punch the bar + shake feel when the city actually takes damage
            if (h01 < lastCityHealth01 - 0.0001f) Juice.Pop(cityPanelRT, 0.18f);
            lastCityHealth01 = h01;
        }

        float shown01 = cityFillRT.anchorMax.x;     // animated value, not the raw target
        cityFillImg.color = shown01 > 0.5f
            ? Color.Lerp(CityMid, CityFull, (shown01 - 0.5f) * 2f)
            : Color.Lerp(CityLow, CityMid, shown01 * 2f);
        cityText.text = $"CIDADE: {Mathf.RoundToInt(shown01 * 100f)}%";

        bool dep = gm.CurrentState == GameManager.GameState.Deployment;
        bool act = gm.CurrentState == GameManager.GameState.Action;
        bool over = gm.CurrentState == GameManager.GameState.GameOver;
        bool vic  = gm.CurrentState == GameManager.GameState.Victory;

        bannerGO.SetActive(dep);
        startButtonGO.SetActive(dep);
        legendGO.SetActive(dep);                              // só na Preparação (tela limpa na Ação)
        // Tutorial só aparece quando o jogador clica no "i" (por ~5s).
        if (infoTimer > 0f) infoTimer -= Time.unscaledDeltaTime;
        controlsGO.SetActive(infoTimer > 0f);
        endGO.SetActive(over || vic);

        // Wave summary: só na Preparação e só se já houve uma onda concluída
        bool showSummary = dep && gm.LastWaveCompleted > 0;
        summaryGO.SetActive(showSummary);
        if (showSummary)
            summaryText.text = $"Onda {gm.LastWaveCompleted} concluida!   " +
                               $"+{gm.LastWaveScore} pontos   |   melhor combo: {gm.BestCombo}   |   " +
                               $"cidade: {Mathf.RoundToInt(gm.CityHealth01 * 100f)}%";

        // Loja de unidades: botão sempre na Preparação; o painel só quando aberto.
        if (!dep) shopOpen = false;
        if (dep && Input.GetKeyDown(KeyCode.Tab)) shopOpen = !shopOpen;
        ShopOpen = dep && shopOpen;

        shopButtonGO.SetActive(dep);
        shopGO.SetActive(dep && shopOpen);
        if (shopButtonText != null) shopButtonText.text = shopOpen ? "FECHAR LOJA (TAB)" : "ABRIR LOJA (TAB)";

        if (dep && shopOpen)
        {
            var sel = deployment != null ? deployment.SelectedType : Collector.UnitType.Collector;
            for (int i = 0; i < 3; i++)
            {
                var t = (Collector.UnitType)i;
                bool selected = (t == sel);
                bool afford = gm.coins >= Collector.CostFor(t);
                bool canBuy = Collector.CanBuy(t);
                Color baseC = Collector.ColorOf(t);
                Color show = (afford && canBuy) ? baseC : Color.Lerp(baseC, Color.gray, 0.6f);
                shopFills[i].color = selected ? show : new Color(show.r, show.g, show.b, 0.7f);
                shopCostTexts[i].text = $"{Collector.CostFor(t)} moedas\n{Collector.CountOf(t)}/{Collector.LimitFor(t)} ativos";
            }
        }

        // Banner pop-in when a new phase begins
        if (gm.CurrentState != lastState)
        {
            lastState = gm.CurrentState;
            if (dep)
            {
                Juice.Pop(bannerGO.transform, 0.12f, 0.3f);
                if (gm.LastWaveCompleted > 0) Juice.Pop(summaryGO.transform, 0.14f, 0.3f);
            }
        }

        // Combo readout — punchy pop whenever it changes
        if (gm.Combo != lastCombo)
        {
            lastCombo = gm.Combo;
            if (gm.Combo > 0)
            {
                comboText.enabled = true;
                comboText.text = $"COMBO x{gm.Multiplier}   ({gm.Combo} seguidos)";
                Juice.Pop(comboRT, 0.4f);
            }
            else comboText.enabled = false;
        }

        // Flash overlay fade-out (unscaled so it works during slow-mo)
        if (flashImg != null && flashImg.color.a > 0f)
        {
            var c = flashImg.color;
            c.a = Mathf.Max(0f, c.a - Time.unscaledDeltaTime * 1.6f);
            flashImg.color = c;
        }

        if (dep)
        {
            bannerSub.text = "Abra a LOJA (TAB) para comprar unidades, feche e clique no mapa p/ posicionar";
        }

        if (over || vic)
        {
            endTitle.text = vic ? "CIDADE SALVA!" : "CIDADE POLUIDA!";
            endPanelImg.color = vic ? new Color(0.3f, 0.8f, 0.35f) : new Color(0.95f, 0.35f, 0.35f);
            endScore.text = $"PONTUACAO: {gm.score}    |    MELHOR COMBO: {gm.BestCombo}";
            if (Input.GetKeyDown(KeyCode.R)) gm.RestartGame();
        }
    }

    // ── UI construction ─────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas + scaler
        var canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGO.transform;

        BuildVignette(root);   // back: darkens the screen edges
        BuildStats(root);
        BuildCityHealth(root);
        BuildLegend(root);
        BuildCombo(root);
        BuildBanner(root);
        BuildWaveSummary(root);
        BuildShop(root);
        BuildControls(root);
        BuildEndScreen(root);
        BuildFlash(root);      // front: colour flash overlay
    }

    void BuildVignette(Transform root)
    {
        var rt = NewUI("Vignette", root);
        Stretch(rt, 0, 0, 0, 0);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = GameAssets.Vignette;
        img.color = new Color(1f, 1f, 1f, 0.9f);
        img.raycastTarget = false;
    }

    void BuildFlash(Transform root)
    {
        var rt = NewUI("Flash", root);
        Stretch(rt, 0, 0, 0, 0);
        flashImg = rt.gameObject.AddComponent<Image>();
        flashImg.color = new Color(1f, 1f, 1f, 0f);   // invisible until Flash() is called
        flashImg.raycastTarget = false;
    }

    // City-health bar — the lose condition, always visible (top-right).
    // Starts full and green; shrinks toward red as the city is damaged.
    void BuildCityHealth(Transform root)
    {
        var fill = Panel(root, out var c, White);
        Set(c, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(560, 150));
        cityPanelRT = c;

        cityText = AddText(fill, "CIDADE: 100%", 32, Black, TextAnchor.UpperCenter);
        ((RectTransform)cityText.transform).offsetMax = new Vector2(-12, -10);

        // dark track
        var track = NewUI("Track", fill);
        track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = new Vector2(0.5f, 0);
        track.offsetMin = new Vector2(18, 18);
        track.offsetMax = new Vector2(-18, 62);          // 44px tall bar
        track.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f);

        // coloured fill — width driven by anchorMax.x each frame (full at start)
        cityFillRT = NewUI("Fill", track);
        cityFillRT.anchorMin = Vector2.zero; cityFillRT.anchorMax = new Vector2(1f, 1f);
        cityFillRT.offsetMin = new Vector2(4, 4); cityFillRT.offsetMax = new Vector2(-4, -4);
        cityFillImg = cityFillRT.gameObject.AddComponent<Image>();
        cityFillImg.color = CityFull;
    }

    void BuildCombo(Transform root)
    {
        comboRT = NewUI("Combo", root);
        // Mesma faixa do banner (nunca aparecem juntos: banner=preparação, combo=ação).
        Set(comboRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -400), new Vector2(900, 90));
        comboText = comboRT.gameObject.AddComponent<Text>();
        comboText.font = font;
        comboText.fontSize = 58;
        comboText.fontStyle = FontStyle.Bold;
        comboText.alignment = TextAnchor.MiddleCenter;
        comboText.color = Yellow;
        comboText.horizontalOverflow = HorizontalWrapMode.Overflow;
        comboText.verticalOverflow = VerticalWrapMode.Overflow;
        AddShadow(comboText);
        comboText.enabled = false;
    }

    void BuildStats(Transform root)
    {
        var fill = Panel(root, out var c, White);
        Set(c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(460, 200));
        statsText = AddText(fill, "", 32, Black, TextAnchor.UpperLeft);
    }

    void BuildLegend(Transform root)
    {
        legendGO = new GameObject("Legend", typeof(RectTransform));
        var lrt = legendGO.GetComponent<RectTransform>();
        lrt.SetParent(root, false);

        var types = new[]
        {
            (TrashItem.TrashType.Glass,   "VIDRO"),
            (TrashItem.TrashType.Plastic, "PLASTICO"),
            (TrashItem.TrashType.Paper,   "PAPEL"),
            (TrashItem.TrashType.Metal,   "METAL"),
        };

        // Legenda compacta (só os nomes), 2ª linha do topo. Aparece só na
        // Preparação — na Ação as lixeiras coloridas já guiam (tela mais limpa).
        float chipW = 200, chipH = 66, gap = 12;
        float totalW = chipW * 4 + gap * 3;
        Set(lrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(totalW, chipH));

        for (int i = 0; i < types.Length; i++)
        {
            Color col = TrashItem.ColorFor(types[i].Item1);
            var fill = Panel(lrt, out var c, col);
            Set(c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(i * (chipW + gap), 0), new Vector2(chipW, chipH));
            AddText(fill, types[i].Item2, 28, Black, TextAnchor.MiddleCenter);
        }
    }

    void BuildBanner(Transform root)
    {
        // Faixa FINA no topo (título + dica) — não cobre o centro da tela.
        var fill = Panel(root, out var c, Yellow);
        Set(c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(1020, 100));
        bannerGO = c.gameObject;

        var title = AddText(fill, "FASE DE PREPARACAO", 30, Black, TextAnchor.UpperCenter);
        ((RectTransform)title.transform).offsetMax = new Vector2(-10, -8);
        bannerSub = AddText(fill, "", 20, Black, TextAnchor.LowerCenter);
        ((RectTransform)bannerSub.transform).offsetMin = new Vector2(10, 8);

        // Botão grande COMECAR ONDA no RODAPÉ-centro (deixa o meio livre p/ posicionar).
        var bf = Panel(root, out var bc, new Color(0.25f, 0.85f, 0.40f));
        Set(bc, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(560, 110));
        startButtonGO = bc.gameObject;
        var btn = bf.gameObject.AddComponent<Button>();
        btn.targetGraphic = bf.GetComponent<Image>();
        btn.onClick.AddListener(() => GameManager.Instance?.BeginWave());
        AddText(bf, "► COMECAR ONDA  (ENTER)", 32, Black, TextAnchor.MiddleCenter);
    }

    // Resumo da onda anterior — faixa fina no topo (abaixo do banner).
    void BuildWaveSummary(Transform root)
    {
        var fill = Panel(root, out var c, Cream);
        Set(c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -424), new Vector2(1020, 78));
        summaryGO = c.gameObject;
        summaryText = AddText(fill, "", 26, Black, TextAnchor.MiddleCenter);
        summaryGO.SetActive(false);
    }

    // Loja de unidades — painel "shopping" que abre/fecha (tela livre p/ posicionar).
    void BuildShop(Transform root)
    {
        var types = new[] { Collector.UnitType.Collector, Collector.UnitType.Magnet, Collector.UnitType.Drone };
        var descs = new[] { "Coleta perto\n(qualquer tipo)", "Raio grande\nSO METAL", "Patrulha o\nmapa todo" };

        // ── Painel modal central (visível só quando a loja está ABERTA) ──
        shopGO = new GameObject("ShopPanel", typeof(RectTransform));
        var prt = shopGO.GetComponent<RectTransform>();
        prt.SetParent(root, false);
        Stretch(prt, 0, 0, 0, 0);
        shopGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);   // escurece o fundo (modal)

        var panelFill = Panel(prt, out var pc, Cream);
        Set(pc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1340, 520));

        var ptitle = AddText(panelFill, "LOJA DE UNIDADES", 44, Black, TextAnchor.UpperCenter);
        ((RectTransform)ptitle.transform).offsetMax = new Vector2(-12, -18);
        AddShadow(ptitle);
        var psub = AddText(panelFill, "Escolha (clique ou 1/2/3) e FECHE para posicionar no mapa.  TAB abre/fecha.", 22, Black, TextAnchor.LowerCenter);
        ((RectTransform)psub.transform).offsetMin = new Vector2(12, 18);

        float cardW = 400, cardH = 320, gap = 28;
        var row = NewUI("Row", panelFill);
        Set(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 6), new Vector2(cardW * 3 + gap * 2, cardH));

        for (int i = 0; i < 3; i++)
        {
            var t = types[i];
            var fill = Panel(row, out var c, Collector.ColorOf(t));
            Set(c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((i - 1) * (cardW + gap), 0), new Vector2(cardW, cardH));

            shopFills[i] = fill.GetComponent<Image>();
            var btn = fill.gameObject.AddComponent<Button>();
            btn.targetGraphic = shopFills[i];
            var tt = t;
            btn.onClick.AddListener(() => { if (deployment != null) deployment.SelectType(tt); shopOpen = false; });

            var name = AddText(fill, $"[{i + 1}] {Collector.NameOf(t)}", 32, Black, TextAnchor.UpperCenter);
            ((RectTransform)name.transform).offsetMax = new Vector2(-8, -14);
            AddText(fill, descs[i], 24, Black, TextAnchor.MiddleCenter);
            shopCostTexts[i] = AddText(fill, "", 26, Black, TextAnchor.LowerCenter);
            ((RectTransform)shopCostTexts[i].transform).offsetMin = new Vector2(8, 14);
        }
        shopGO.SetActive(false);

        // ── Botão toggle (canto inferior direito) ──
        var btnFill = Panel(root, out var bc, Yellow);
        Set(bc, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(440, 110));
        shopButtonGO = bc.gameObject;
        var sbtn = btnFill.gameObject.AddComponent<Button>();
        sbtn.targetGraphic = btnFill.GetComponent<Image>();
        sbtn.onClick.AddListener(() => shopOpen = !shopOpen);
        shopButtonText = AddText(btnFill, "ABRIR LOJA (TAB)", 30, Black, TextAnchor.MiddleCenter);
    }

    void BuildControls(Transform root)
    {
        // Painel de ajuda — escondido por padrão; aparece por ~5s ao clicar no "i".
        var fill = Panel(root, out var c, Cream);
        Set(c, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 180), new Vector2(1100, 230));
        controlsGO = c.gameObject;
        AddText(fill,
            "MOVER: A / D ou setas\n" +
            "ESPACO (ou E): pega o lixo mais proximo / entrega na lixeira\n" +
            "Lixeira ERRADA descarta o lixo (-20, mas libera a mao)\n" +
            "Combo = acertos seguidos (x ate 5). Anel dourado = 3x.",
            28, Black, TextAnchor.MiddleLeft);
        controlsGO.SetActive(false);

        // Botão "i" pequeno no canto inferior esquerdo (sempre visível).
        var iFill = Panel(root, out var ic, Cream);
        Set(ic, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(80, 80));
        var ibtn = iFill.gameObject.AddComponent<Button>();
        ibtn.targetGraphic = iFill.GetComponent<Image>();
        ibtn.onClick.AddListener(() => infoTimer = 5f);
        AddText(iFill, "i", 48, Black, TextAnchor.MiddleCenter);
    }

    void BuildEndScreen(Transform root)
    {
        // full-screen dim
        endGO = new GameObject("EndScreen", typeof(RectTransform));
        var ert = endGO.GetComponent<RectTransform>();
        ert.SetParent(root, false);
        Stretch(ert, 0, 0, 0, 0);
        var dim = endGO.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.65f);

        var fill = Panel(ert, out var c, new Color(0.3f, 0.8f, 0.35f));
        Set(c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 460));
        endPanelImg = fill.GetComponent<Image>();

        endTitle = AddText(fill, "VOCE VENCEU!", 84, Black, TextAnchor.UpperCenter);
        ((RectTransform)endTitle.transform).offsetMax = new Vector2(-20, -36);
        AddShadow(endTitle);

        endScore = AddText(fill, "", 46, Black, TextAnchor.MiddleCenter);

        // restart button (brutalist)
        var btnFill = Panel(fill, out var bc, Yellow);
        Set(bc, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(360, 100));
        var btn = btnFill.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnFill.GetComponent<Image>();
        btn.onClick.AddListener(() => { if (gm != null) gm.RestartGame(); });
        AddText(btnFill, "JOGAR DE NOVO  (R)", 34, Black, TextAnchor.MiddleCenter);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // A brutalist panel: hard shadow + thick black border + coloured fill.
    // Returns the fill RectTransform (add content to it). 'container' is the
    // outer RectTransform you position via Set().
    RectTransform Panel(Transform parent, out RectTransform container, Color fillColor)
    {
        container = NewUI("Panel", parent);

        var shadow = NewUI("Shadow", container);
        Stretch(shadow, SHADOW, -SHADOW, -SHADOW, SHADOW); // shifted down-right
        shadow.offsetMin = new Vector2(SHADOW, -SHADOW);
        shadow.offsetMax = new Vector2(SHADOW, -SHADOW);
        shadow.gameObject.AddComponent<Image>().color = Black;

        var border = NewUI("Border", container);
        Stretch(border, 0, 0, 0, 0);
        border.gameObject.AddComponent<Image>().color = Black;

        var fill = NewUI("Fill", container);
        Stretch(fill, BORDER, BORDER, BORDER, BORDER);
        fill.gameObject.AddComponent<Image>().color = fillColor;

        return fill;
    }

    RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    Text AddText(Transform parent, string s, int size, Color color, TextAnchor anchor)
    {
        var rt = NewUI("Text", parent);
        Stretch(rt, 14, 10, 14, 10);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    void AddShadow(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, 0.35f);
        s.effectDistance = new Vector2(3, -3);
    }

    // Position helper for non-stretch elements.
    void Set(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    // Stretch to fill parent with insets (left, bottom, right, top).
    void Stretch(RectTransform rt, float l, float b, float r, float t)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    void BuildEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<StandaloneInputModule>();
    }
}
