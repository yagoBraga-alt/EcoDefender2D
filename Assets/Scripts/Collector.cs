using UnityEngine;
using DG.Tweening;

// "Secret unit deployment" (Mecânica 3) — agora com 3 tipos compráveis com moedas.
//   • Coletor Automático: estático, raio pequeno, qualquer tipo.
//   • Ímã de Metal: estático, raio grande, SÓ metal.
//   • Drone Ambiental: patrulha o mapa coletando qualquer tipo.
// Cada unidade é uma rede de segurança limitada (enche e para), comprada na fase
// de Preparação pelo DeploymentController.
[RequireComponent(typeof(CircleCollider2D))]
public class Collector : MonoBehaviour
{
    public enum UnitType { Collector, Magnet, Drone }

    [Header("Type")]
    public UnitType type = UnitType.Collector;

    [Header("Patrol (apenas Drone)")]
    public float patrolMinX = -8f, patrolMaxX = 8f;

    // ── Tabela estática (também usada pela loja/HUD e pelo ghost) ───────────────
    public static int    CostFor(UnitType t)   => t switch { UnitType.Collector => 30, UnitType.Magnet => 45, UnitType.Drone => 70, _ => 30 };
    public static float  RadiusOf(UnitType t)  => t switch { UnitType.Collector => 0.9f, UnitType.Magnet => 2.2f, UnitType.Drone => 1.2f, _ => 0.9f };
    public static string NameOf(UnitType t)    => t switch { UnitType.Collector => "COLETOR", UnitType.Magnet => "IMA DE METAL", UnitType.Drone => "DRONE", _ => "" };
    public static Color  ColorOf(UnitType t)   => t switch
    {
        UnitType.Collector => new Color(0.10f, 0.70f, 0.95f),  // azul
        UnitType.Magnet    => new Color(1.00f, 0.50f, 0.10f),  // laranja
        UnitType.Drone     => new Color(0.20f, 0.85f, 0.45f),  // verde
        _ => Color.white
    };

    // Limite de quantas unidades de cada tipo podem existir ao mesmo tempo.
    public static int LimitFor(UnitType t) => t switch
    {
        UnitType.Collector => 3, UnitType.Magnet => 2, UnitType.Drone => 1, _ => 1
    };

    // Contagem de unidades ativas por tipo (resetada a cada Play).
    static int[] activeCounts = new int[3];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCounts() => activeCounts = new int[3];
    public static int  CountOf(UnitType t) => activeCounts[(int)t];
    public static bool CanBuy(UnitType t)  => activeCounts[(int)t] < LimitFor(t);

    // Stats derivados do tipo
    private float radius; private int capacity; private int pointsPerTrash;
    private float cooldown; private bool metalOnly; private bool moves; private float moveSpeed;

    private int collected; private float timer; private bool full; private int dir = 1;
    private SpriteRenderer body, ringSr; private CircleCollider2D col;

    static readonly Color FullColor = new Color(0.45f, 0.45f, 0.45f);

    void ApplyStats()
    {
        radius = RadiusOf(type);
        switch (type)
        {
            case UnitType.Collector: capacity = 3; pointsPerTrash = 25; cooldown = 0.40f; metalOnly = false; moves = false; break;
            case UnitType.Magnet:    capacity = 5; pointsPerTrash = 30; cooldown = 0.30f; metalOnly = true;  moves = false; break;
            case UnitType.Drone:     capacity = 6; pointsPerTrash = 25; cooldown = 0.25f; metalOnly = false; moves = true;  moveSpeed = 4f; break;
        }
    }

    void Start()
    {
        ApplyStats();
        activeCounts[(int)type]++;

        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        Color c = ColorOf(type);
        body = GetComponent<SpriteRenderer>();
        if (body)
        {
            body.sprite = GameAssets.Circle;
            body.color = c;
            body.sortingOrder = 2;
        }

        // anel mostrando o raio de ação
        var ringGo = new GameObject("Radius");
        ringGo.transform.SetParent(transform);
        ringGo.transform.localPosition = Vector3.zero;
        ringSr = ringGo.AddComponent<SpriteRenderer>();
        ringSr.sprite = GameAssets.MakeRing(96, 6);
        ringSr.color = new Color(c.r, c.g, c.b, 0.5f);
        ringSr.sortingOrder = 1;
        ringGo.transform.localScale = Vector3.one * (radius * 2f);

        // o drone precisa de rigidbody p/ triggers corretos enquanto se move
        if (moves)
        {
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        Juice.Pop(transform, 0.3f);
    }

    void Update()
    {
        if (timer > 0f) timer -= Time.deltaTime;

        // Drone patrulha horizontalmente durante a ação
        if (moves && !full && GameManager.Instance?.CurrentState == GameManager.GameState.Action)
        {
            var p = transform.position;
            p.x += dir * moveSpeed * Time.deltaTime;
            if (p.x >= patrolMaxX) { p.x = patrolMaxX; dir = -1; }
            else if (p.x <= patrolMinX) { p.x = patrolMinX; dir = 1; }
            transform.position = p;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (full || timer > 0f) return;
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Action) return;

        TrashItem item = other.GetComponent<TrashItem>();
        if (item == null || item.IsCollected || item.IsStuck) return;
        if (metalOnly && item.trashType != TrashItem.TrashType.Metal) return;   // ímã só pega metal

        int pts = item.isGolden ? pointsPerTrash * 3 : pointsPerTrash;
        item.Collect();
        GameManager.Instance?.AddScore(pts);
        // Unidades NÃO geram moedas (evita o loop "comprar coletor → lucrar → comprar mais").
        // Só a reciclagem MANUAL rende moedas.

        Color c = ColorOf(type);
        Juice.Burst(other.transform.position, TrashItem.ColorFor(item.trashType), 8, 4f);
        Juice.Text(transform.position + Vector3.up * 0.6f, $"+{pts}", c, 0.8f);
        Juice.Pop(transform, 0.3f);
        AudioManager.Instance?.PlayCollect();

        item.Deliver();

        collected++;
        timer = cooldown;
        if (collected >= capacity) Deplete();
    }

    // Cheia: fica cinza e para de coletar.
    void Deplete()
    {
        full = true;
        col.enabled = false;
        if (body)   body.color = FullColor;
        if (ringSr) ringSr.color = new Color(FullColor.r, FullColor.g, FullColor.b, 0.25f);
        Juice.Pop(transform, 0.25f);
        Juice.Burst(transform.position, FullColor, 6, 3f);
        Destroy(gameObject, 1.2f);   // some após encher → libera o slot do limite
    }

    void OnDestroy()
    {
        activeCounts[(int)type]--;
        DOTween.Kill(transform);
    }
}
