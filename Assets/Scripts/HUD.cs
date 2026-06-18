using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Neo-brutalist HUD built with Unity's uGUI components (Canvas, Image, Text,
// Button) and a CanvasScaler so text stays big and crisp at any resolution.
// Style: flat bright panels, thick black borders, hard offset shadows.
public class HUD : MonoBehaviour
{
    private Font font;
    private GameManager gm;
    private DeploymentController deployment;

    // Dynamic references
    private Text statsText, bannerSub, endTitle, endScore;
    private GameObject bannerGO, legendGO, controlsGO, endGO;
    private Image endPanelImg;

    const float SHADOW = 12f, BORDER = 6f;
    static readonly Color Black  = Color.black;
    static readonly Color White  = Color.white;
    static readonly Color Yellow = new Color(1f, 0.85f, 0.1f);
    static readonly Color Cream  = new Color(0.98f, 0.96f, 0.88f);

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deployment = FindAnyObjectByType<DeploymentController>();
        BuildEventSystem();
        BuildUI();
    }

    void Update()
    {
        gm = GameManager.Instance;
        if (gm == null) return;

        statsText.text = $"PONTOS: {gm.score}\nVIDAS: {Mathf.Max(0, gm.lives)}\nONDA: {gm.GetCurrentWave()}/{gm.totalWaves}";

        bool dep = gm.CurrentState == GameManager.GameState.Deployment;
        bool act = gm.CurrentState == GameManager.GameState.Action;
        bool over = gm.CurrentState == GameManager.GameState.GameOver;
        bool vic  = gm.CurrentState == GameManager.GameState.Victory;

        bannerGO.SetActive(dep);
        legendGO.SetActive(dep || act);
        controlsGO.SetActive(act);
        endGO.SetActive(over || vic);

        if (dep)
        {
            int left = deployment != null ? deployment.CollectorsRemaining : 0;
            bannerSub.text = $"Clique no mapa para posicionar coletores (restam: {left})\n" +
                             $"A onda comeca em {Mathf.CeilToInt(gm.GetDeploymentTimeRemaining())}s";
        }

        if (over || vic)
        {
            endTitle.text = vic ? "VOCE VENCEU!" : "GAME OVER";
            endPanelImg.color = vic ? new Color(0.3f, 0.8f, 0.35f) : new Color(0.95f, 0.35f, 0.35f);
            endScore.text = $"PONTUACAO FINAL: {gm.score}";
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

        BuildStats(root);
        BuildLegend(root);
        BuildBanner(root);
        BuildControls(root);
        BuildEndScreen(root);
    }

    void BuildStats(Transform root)
    {
        var fill = Panel(root, out var c, White);
        Set(c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(460, 190));
        statsText = AddText(fill, "", 40, Black, TextAnchor.UpperLeft);
    }

    void BuildLegend(Transform root)
    {
        legendGO = new GameObject("Legend", typeof(RectTransform));
        var lrt = legendGO.GetComponent<RectTransform>();
        lrt.SetParent(root, false);

        var types = new[]
        {
            (TrashItem.TrashType.Organic, "ORGANICO", "restos, cascas"),
            (TrashItem.TrashType.Plastic, "PLASTICO", "garrafas, sacolas"),
            (TrashItem.TrashType.Paper,   "PAPEL",    "jornais, caixas"),
            (TrashItem.TrashType.Metal,   "METAL",    "latas, tampas"),
        };

        float chipW = 300, chipH = 130, gap = 18;
        float totalW = chipW * 4 + gap * 3;
        Set(lrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(totalW, chipH));

        for (int i = 0; i < types.Length; i++)
        {
            Color col = TrashItem.ColorFor(types[i].Item1);
            var fill = Panel(lrt, out var c, col);
            Set(c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(i * (chipW + gap), 0), new Vector2(chipW, chipH));
            var name = AddText(fill, types[i].Item2, 36, Black, TextAnchor.UpperCenter);
            ((RectTransform)name.transform).offsetMax = new Vector2(-10, -12);
            var ex = AddText(fill, types[i].Item3, 24, Black, TextAnchor.LowerCenter);
            ((RectTransform)ex.transform).offsetMin = new Vector2(10, 12);
        }
    }

    void BuildBanner(Transform root)
    {
        var fill = Panel(root, out var c, Yellow);
        Set(c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(1150, 180));
        bannerGO = c.gameObject;

        var title = AddText(fill, "FASE DE PREPARACAO", 56, Black, TextAnchor.UpperCenter);
        ((RectTransform)title.transform).offsetMax = new Vector2(-12, -14);
        AddShadow(title);
        bannerSub = AddText(fill, "", 32, Black, TextAnchor.LowerCenter);
        ((RectTransform)bannerSub.transform).offsetMin = new Vector2(12, 16);
    }

    void BuildControls(Transform root)
    {
        var fill = Panel(root, out var c, Cream);
        Set(c, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(960, 210));
        controlsGO = c.gameObject;
        AddText(fill,
            "MOVER: A / D ou setas\n" +
            "PEGAR LIXO: encoste nele\n" +
            "ENTREGAR: E ou ESPACO perto da lixeira CERTA\n" +
            "Nao deixe o lixo grudar no chao!",
            32, Black, TextAnchor.MiddleLeft);
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
