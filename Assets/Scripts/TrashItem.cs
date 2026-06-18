using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TrashItem : MonoBehaviour
{
    public enum TrashType { Organic, Plastic, Paper, Metal }

    [Header("Type")]
    public TrashType trashType;

    [Header("Fall Settings")]
    public float fallSpeed = 3f;

    [Header("Sticky Settings")]
    public float timeBeforeStick = 5f;   // seconds on ground before it sticks
    public Color stickyColor = new Color(0.4f, 0.2f, 0f); // visual cue when stuck

    // State
    public bool IsCollected { get; private set; }
    public bool IsStuck { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private bool isGrounded;
    private float groundTimer;
    private Color originalColor;

    // Color coding per type (used by TrashSpawner to tint sprites)
    public static Color ColorFor(TrashType t) => t switch
    {
        TrashType.Organic => new Color(0.30f, 0.80f, 0.20f), // punchy green
        TrashType.Plastic => new Color(0.95f, 0.25f, 0.30f), // bold red (BR plastic = red)
        TrashType.Paper   => new Color(0.20f, 0.55f, 1.00f), // bold blue (BR paper = blue)
        TrashType.Metal   => new Color(1.00f, 0.80f, 0.10f), // bold yellow (BR metal = yellow)
        _ => Color.white
    };

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        rb.gravityScale = 0f;           // we control fall manually
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        originalColor = ColorFor(trashType);
        if (sr) sr.color = originalColor;
    }

    void Update()
    {
        if (IsCollected || IsStuck) return;

        if (!isGrounded)
        {
            // Manual fall so we don't need to fight the Microgame's physics layers
            rb.linearVelocity = Vector2.down * fallSpeed;
            // Gentle spin while falling for a livelier look
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            groundTimer += Time.deltaTime;

            // Visual warning: lerp colour toward sticky brown as time runs out
            float t = groundTimer / timeBeforeStick;
            if (sr) sr.color = Color.Lerp(originalColor, stickyColor, t);

            if (groundTimer >= timeBeforeStick)
                Stick();
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<Ground>() != null)
            isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<Ground>() != null)
        {
            isGrounded = false;
            groundTimer = 0f;
        }
    }

    // Called by Player when picked up
    public void Collect()
    {
        if (IsStuck || IsCollected) return;
        IsCollected = true;
        gameObject.SetActive(false);
    }

    // Called when trash sticks to the ground (wrong outcome)
    void Stick()
    {
        IsStuck = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        if (sr) sr.color = stickyColor;
        GameManager.Instance?.LoseLife();
        TrashSpawner.Instance?.NotifyTrashResolved();
        AudioManager.Instance?.PlayStick();
        Debug.Log($"[TrashItem] {trashType} stuck to ground! Life lost.");
    }

    // Called by Player after delivering to correct bin
    public void Deliver()
    {
        TrashSpawner.Instance?.NotifyTrashResolved();
        Destroy(gameObject);
    }
}
