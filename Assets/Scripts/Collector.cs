using UnityEngine;

// "Secret unit deployment" mechanic (Mecânica 3).
// A collector is a LIMITED safety net placed before each wave. It catches a
// few items that fall in its (small) radius, then fills up and stops working.
// This buys you time / saves lives, but it can't cover the whole map — you
// still have to play actively to handle most of the trash.
[RequireComponent(typeof(CircleCollider2D))]
public class Collector : MonoBehaviour
{
    [Header("Balance")]
    public float radius = 0.8f;        // small — covers only a narrow column
    public int capacity = 2;           // how many items before it's full
    public int pointsPerTrash = 25;    // much less than manual delivery (100)
    public float cooldown = 0.5f;

    private int collected;
    private float timer;
    private bool full;

    private SpriteRenderer body;
    private SpriteRenderer ringSr;
    private CircleCollider2D col;

    void Start()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        body = GetComponent<SpriteRenderer>();
        if (body)
        {
            body.sprite = GameAssets.Circle;
            body.color = AvailableColor;
            body.sortingOrder = 2;
        }

        var ringGo = new GameObject("Radius");
        ringGo.transform.SetParent(transform);
        ringGo.transform.localPosition = Vector3.zero;
        ringSr = ringGo.AddComponent<SpriteRenderer>();
        ringSr.sprite = GameAssets.MakeRing(96, 6);
        ringSr.color = new Color(AvailableColor.r, AvailableColor.g, AvailableColor.b, 0.5f);
        ringSr.sortingOrder = 1;
        ringGo.transform.localScale = Vector3.one * (radius * 2f);
    }

    void Update()
    {
        if (timer > 0f) timer -= Time.deltaTime;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (full || timer > 0f) return;
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Action) return;

        TrashItem item = other.GetComponent<TrashItem>();
        if (item == null || item.IsCollected || item.IsStuck) return;

        // Catch + auto-sort this one item.
        item.Collect();
        GameManager.Instance?.AddScore(pointsPerTrash);
        item.Deliver();

        collected++;
        timer = cooldown;
        if (collected >= capacity) Deplete();
    }

    // Collector is full: grey out, stop catching.
    void Deplete()
    {
        full = true;
        col.enabled = false;
        if (body)   body.color = FullColor;
        if (ringSr) ringSr.color = new Color(FullColor.r, FullColor.g, FullColor.b, 0.25f);
    }

    static readonly Color AvailableColor = new Color(0.1f, 0.85f, 0.85f); // cyan
    static readonly Color FullColor      = new Color(0.45f, 0.45f, 0.45f); // grey
}
