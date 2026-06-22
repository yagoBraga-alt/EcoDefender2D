using UnityEngine;
using UnityEngine.EventSystems;

// Fase de Preparação: o jogador ESCOLHE um tipo de unidade (teclas 1/2/3 ou os
// cards da loja na HUD) e clica no mapa para COMPRÁ-LA com moedas e posicioná-la.
// Um "ghost" translúcido segue o mouse mostrando cor e raio do tipo selecionado.
public class DeploymentController : MonoBehaviour
{
    [Header("Área de posicionamento")]
    public float minX = -8f, maxX = 8f;
    public float minY = -3f, maxY = 4f;

    public Collector.UnitType SelectedType { get; private set; } = Collector.UnitType.Collector;

    private Camera cam;
    private GameObject ghost;
    private SpriteRenderer ghostBody, ghostRing;

    void Start()
    {
        cam = Camera.main;
        BuildGhost();
        RefreshGhostVisual();
    }

    void BuildGhost()
    {
        ghost = new GameObject("UnitGhost");
        ghostBody = ghost.AddComponent<SpriteRenderer>();
        ghostBody.sprite = GameAssets.Circle;
        ghostBody.sortingOrder = 4;

        var ring = new GameObject("GhostRadius");
        ring.transform.SetParent(ghost.transform);
        ring.transform.localPosition = Vector3.zero;
        ghostRing = ring.AddComponent<SpriteRenderer>();
        ghostRing.sprite = GameAssets.MakeRing(96, 6);
        ghostRing.sortingOrder = 3;

        ghost.SetActive(false);
    }

    // Chamado pelas teclas e pelos cards da loja (HUD).
    public void SelectType(Collector.UnitType t)
    {
        SelectedType = t;
        RefreshGhostVisual();
    }

    void RefreshGhostVisual()
    {
        Color c = Collector.ColorOf(SelectedType);
        float r = Collector.RadiusOf(SelectedType);
        ghostBody.color = new Color(c.r, c.g, c.b, 0.5f);
        ghostRing.color = new Color(c.r, c.g, c.b, 0.45f);
        ghostRing.transform.localScale = Vector3.one * (r * 2f);
    }

    void Update()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Deployment)
        {
            ghost.SetActive(false);
            return;
        }

        // teclas selecionam o tipo mesmo com a loja aberta
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectType(Collector.UnitType.Collector);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectType(Collector.UnitType.Magnet);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectType(Collector.UnitType.Drone);

        // Com a loja ABERTA, a tela fica livre só p/ navegar a loja (não posiciona).
        bool canPlace = !HUD.ShopOpen;
        ghost.SetActive(canPlace);
        if (!canPlace) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        world.x = Mathf.Clamp(world.x, minX, maxX);
        world.y = Mathf.Clamp(world.y, minY, maxY);
        ghost.transform.position = world;

        // ghost verde se dá p/ comprar; vermelho se sem moedas OU no limite
        bool ok = (GameManager.Instance?.coins ?? 0) >= Collector.CostFor(SelectedType)
                  && Collector.CanBuy(SelectedType);
        Color baseC = Collector.ColorOf(SelectedType);
        ghostBody.color = ok ? new Color(baseC.r, baseC.g, baseC.b, 0.55f)
                             : new Color(0.90f, 0.20f, 0.20f, 0.45f);

        // clique no MAPA (ignora cliques sobre a UI)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            TryPlace(world);
    }

    bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void TryPlace(Vector3 pos)
    {
        // limite por tipo
        if (!Collector.CanBuy(SelectedType))
        {
            Juice.Text(pos, $"LIMITE: {Collector.NameOf(SelectedType)} (max {Collector.LimitFor(SelectedType)})",
                       new Color(0.95f, 0.3f, 0.3f), 1f);
            Juice.Shake(0.1f, 0.12f);
            AudioManager.Instance?.PlayDeliverBad();
            return;
        }

        int cost = Collector.CostFor(SelectedType);
        if (GameManager.Instance == null || !GameManager.Instance.TrySpendCoins(cost))
        {
            Juice.Text(pos, "SEM MOEDAS!", new Color(0.95f, 0.3f, 0.3f), 1f);
            Juice.Shake(0.12f, 0.15f);
            AudioManager.Instance?.PlayDeliverBad();
            return;
        }

        var go = new GameObject(Collector.NameOf(SelectedType));
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();      // Collector.Start preenche sprite/cor/anel
        var c = go.AddComponent<Collector>();
        c.type = SelectedType;
        c.patrolMinX = minX; c.patrolMaxX = maxX;

        Color col = Collector.ColorOf(SelectedType);
        Juice.Pop(go.transform, 0.45f);
        Juice.Burst(pos, col, 10, 4f);
        Juice.Text(pos + Vector3.up * 0.8f, Collector.NameOf(SelectedType), col, 0.9f);
        Juice.ZoomPunch(0.04f);
        Juice.Shake(0.1f, 0.15f);
        AudioManager.Instance?.PlayPlace();
    }
}
