using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float minX = -8f;            // play-area horizontal bounds
    public float maxX =  8f;

    [Header("Held Trash")]
    public Transform holdPoint;         // empty child Transform above the player sprite

    // Internal state
    private Rigidbody2D rb;
    private TrashItem heldTrash;
    private bool facingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;           // top-down / scroller — no jumping
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
    }

    void Update()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Action)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleMovement();

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            TryDeliver();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(h * moveSpeed, rb.linearVelocity.y);

        // Keep the player inside the play area without needing physical walls
        Vector3 p = transform.position;
        if (p.x < minX) { p.x = minX; transform.position = p; }
        if (p.x > maxX) { p.x = maxX; transform.position = p; }

        if (h > 0 && !facingRight)  Flip();
        if (h < 0 &&  facingRight)  Flip();
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    // ── Pickup (automatic on touch) ───────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (heldTrash != null) return; // already carrying something

        TrashItem item = other.GetComponent<TrashItem>();
        if (item == null || item.IsCollected || item.IsStuck) return;

        item.Collect();
        heldTrash = item;

        // Attach visually to holdPoint (or above player if no holdPoint)
        item.gameObject.SetActive(true);
        item.transform.SetParent(holdPoint != null ? holdPoint : transform);
        item.transform.localPosition = Vector3.zero;

        // Freeze the item's physics while carried
        var itemRb = item.GetComponent<Rigidbody2D>();
        if (itemRb) itemRb.bodyType = RigidbodyType2D.Kinematic;

        AudioManager.Instance?.PlayCollect();
        Debug.Log($"[Player] Picked up {item.trashType}");
    }

    // ── Deliver ───────────────────────────────────────────────────────────────

    void TryDeliver()
    {
        if (heldTrash == null) return;

        // Look for a bin we're overlapping
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1f);
        foreach (var hit in hits)
        {
            RecyclingBin bin = hit.GetComponent<RecyclingBin>();
            if (bin == null) continue;

            if (bin.AcceptedType == heldTrash.trashType)
            {
                // Correct bin
                GameManager.Instance?.AddScore(100);
                heldTrash.transform.SetParent(null);
                heldTrash.Deliver();
                heldTrash = null;
                AudioManager.Instance?.PlayDeliverGood();
                Debug.Log("[Player] Correct bin! +100");
            }
            else
            {
                // Wrong bin — penalise
                GameManager.Instance?.AddScore(-20);
                AudioManager.Instance?.PlayDeliverBad();
                Debug.Log("[Player] Wrong bin! -20");
            }
            return;
        }

        // No bin nearby — drop trash back onto the ground
        if (heldTrash != null)
        {
            heldTrash.transform.SetParent(null);
            heldTrash.transform.position = transform.position + Vector3.down * 0.8f;
            var itemRb = heldTrash.GetComponent<Rigidbody2D>();
            if (itemRb) itemRb.bodyType = RigidbodyType2D.Dynamic;
            heldTrash = null;
        }
    }

    // ── Accessors ─────────────────────────────────────────────────────────────

    public bool IsCarrying => heldTrash != null;
    public TrashItem.TrashType? HeldType => heldTrash?.trashType;
}
