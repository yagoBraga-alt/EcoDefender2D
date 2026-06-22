using UnityEngine;

// Falling helper pickups. Catch one (just touch it) to trigger a beneficial
// effect. Built, animated and balanced entirely in code, consistent with the
// rest of EcoDefender. Spawned occasionally by TrashSpawner during a wave.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class PowerUp : MonoBehaviour
{
    public enum Kind { Cleanup, SlowMotion, Scrub }

    public Kind kind;
    public float fallSpeed = 2f;
    public float despawnY = -6f;

    static Color ColorFor(Kind k) => k switch
    {
        Kind.Cleanup    => new Color(0.30f, 0.90f, 0.40f),  // green
        Kind.SlowMotion => new Color(0.30f, 0.60f, 1.00f),  // blue
        Kind.Scrub      => new Color(0.10f, 0.85f, 0.85f),  // cyan
        _ => Color.white
    };
    static string LabelFor(Kind k) => k switch
    {
        Kind.Cleanup    => "LIMPEZA",
        Kind.SlowMotion => "LENTO",
        Kind.Scrub      => "ANTI-POLUICAO",
        _ => ""
    };

    void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
    }

    void Start()
    {
        Color c = ColorFor(kind);

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GameAssets.Circle;
        sr.color = c;
        sr.sortingOrder = 4;

        // pulsing white ring so it clearly reads as "special"
        var ring = new GameObject("Ring");
        ring.transform.SetParent(transform);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = Vector3.one * 1.7f;
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = GameAssets.MakeRing(64, 6);
        rs.color = Color.white;
        rs.sortingOrder = 3;
        ring.AddComponent<Pulse>().amount = 0.14f;

        Juice.Pop(transform, 0.4f);
        Juice.Text(transform.position + Vector3.up * 0.8f, LabelFor(kind), c, 0.9f, 0.4f, 1.2f);
    }

    void Update()
    {
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
        if (transform.position.y < despawnY) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Player>() == null) return;   // only the player collects it

        Apply();
        Juice.Burst(transform.position, ColorFor(kind), 16, 5f);
        Juice.Shake(0.15f, 0.2f);
        Juice.ZoomPunch(0.05f);
        AudioManager.Instance?.PlayPowerUp();
        Destroy(gameObject);
    }

    void Apply()
    {
        var gm = GameManager.Instance;
        switch (kind)
        {
            case Kind.Cleanup:
                CleanupAllTrash();
                gm?.HealCity(10f);
                Juice.Text(transform.position, "LIMPEZA!", ColorFor(kind), 1.4f);
                break;
            case Kind.SlowMotion:
                gm?.SlowTrash(5f, 0.4f);
                Juice.Text(transform.position, "CAMERA LENTA!", ColorFor(kind), 1.4f);
                break;
            case Kind.Scrub:
                gm?.HealCity(30f);
                Juice.Text(transform.position, "+30 CIDADE", ColorFor(kind), 1.4f);
                break;
        }
    }

    // Resolve every active trash on screen (counts toward the wave, gives points).
    void CleanupAllTrash()
    {
        var all = Object.FindObjectsByType<TrashItem>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t == null || t.IsCollected || t.IsStuck) continue;
            Juice.Burst(t.transform.position, TrashItem.ColorFor(t.trashType), 6, 3f);
            GameManager.Instance?.AddScore(20);
            t.Collect();
            t.Deliver();
        }
    }
}
