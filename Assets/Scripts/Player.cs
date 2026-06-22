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
    public float pickupRange = 2.6f;    // raio para pegar o lixo mais próximo ao clicar

    // Internal state
    private Rigidbody2D rb;
    private TrashItem heldTrash;
    private float facing = 1f;     // +1 right, -1 left
    private float punch = 0f;      // transient scale spike from actions

    static readonly Color Gold = new Color(1f, 0.85f, 0.15f);

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

        // ESPAÇO / E — ação única contextual:
        //   mão vazia  → pega o lixo mais próximo
        //   carregando → entrega na lixeira (certa = pontos; errada = descarta)
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
        {
            if (heldTrash == null) TryPickup();
            else TryDeliver();
        }
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

        if (h > 0.01f) facing = 1f;
        else if (h < -0.01f) facing = -1f;
    }

    // ── Squash & stretch (game feel) ───────────────────────────────────────────
    // Drives flip + breathing + a movement stretch + a transient action "punch".
    void LateUpdate()
    {
        float moving = Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.x) / Mathf.Max(0.1f, moveSpeed));
        float breath = 1f + Mathf.Sin(Time.time * 9f) * 0.025f * (0.4f + moving);
        float sx = (1f + moving * 0.10f + punch) * facing;
        float sy = (1f - moving * 0.06f + punch) * breath;
        transform.localScale = new Vector3(sx, sy, 1f);
        punch = Mathf.Lerp(punch, 0f, Time.deltaTime * 8f);
    }

    public void Punch(float amount) => punch = Mathf.Max(punch, amount);

    // ── Pickup (clicar pega o lixo mais próximo) ──────────────────────────────

    void TryPickup()
    {
        if (heldTrash != null) return;   // já carregando

        // Escolhe, dentro do alcance, o lixo mais próximo do jogador (ignora os já
        // coletados/grudados). Assim dá pra mirar o que está quase vencendo.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickupRange);
        TrashItem best = null;
        float bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            TrashItem item = h.GetComponent<TrashItem>();
            if (item == null || item.IsCollected || item.IsStuck) continue;
            float d = Vector2.Distance(transform.position, item.transform.position);
            if (d < bestDist) { bestDist = d; best = item; }
        }
        if (best != null) PickUp(best);
    }

    void PickUp(TrashItem item)
    {
        item.Collect();
        heldTrash = item;

        item.gameObject.SetActive(true);
        item.transform.SetParent(holdPoint != null ? holdPoint : transform);
        item.transform.localPosition = Vector3.zero;

        var itemRb = item.GetComponent<Rigidbody2D>();
        if (itemRb) itemRb.bodyType = RigidbodyType2D.Kinematic;

        Punch(0.22f);
        Juice.Pop(item.transform, 0.3f);
        Juice.Burst(transform.position + Vector3.up * 0.4f, TrashItem.ColorFor(item.trashType), 6, 3f, 0.12f, 0.4f);
        AudioManager.Instance?.PlayCollect();
    }

    // ── Deliver ───────────────────────────────────────────────────────────────

    void TryDeliver()
    {
        if (heldTrash == null) return;

        // Look for a bin we're overlapping
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.4f);
        foreach (var hit in hits)
        {
            RecyclingBin bin = hit.GetComponent<RecyclingBin>();
            if (bin == null) continue;

            Vector3 at = bin.transform.position + Vector3.up * 1.3f;
            var binSr = bin.GetComponentInChildren<SpriteRenderer>();

            if (bin.AcceptedType == heldTrash.trashType)
            {
                // Correct bin → score × combo multiplier (× golden bonus)
                int mult = GameManager.Instance != null ? GameManager.Instance.RegisterGoodDelivery() : 1;
                int pts = 100 * mult;
                if (heldTrash.isGolden) pts *= 3;
                GameManager.Instance?.AddScore(pts);

                // Moedas por tipo (PDF): viram a economia para comprar unidades.
                int gainedCoins = TrashItem.CoinsFor(heldTrash.trashType) * (heldTrash.isGolden ? 2 : 1);
                GameManager.Instance?.AddCoins(gainedCoins);

                Juice.Text(at, $"+{pts}", heldTrash.isGolden ? Gold : new Color(0.25f, 0.95f, 0.35f), 1f);
                Juice.Text(at + Vector3.up * 0.7f, $"+{gainedCoins} moedas", Gold, 0.8f);
                if (mult > 1)
                    Juice.Text(at + Vector3.up * 1.4f, $"COMBO x{mult}", Gold, 1.15f);
                Juice.Burst(bin.transform.position + Vector3.up, TrashItem.ColorFor(heldTrash.trashType), 14, 5f);
                Juice.Pop(bin.transform, 0.22f);
                Juice.Flash(binSr, Color.white, 0.1f);
                Juice.Shake(0.12f, 0.18f);
                Juice.ZoomPunch(0.05f);
                Punch(0.4f);

                AudioManager.Instance?.PlayDeliverGood();
                AudioManager.Instance?.PlayCombo(GameManager.Instance != null ? GameManager.Instance.Combo : 1);

                heldTrash.transform.SetParent(null);
                heldTrash.Deliver();
                heldTrash = null;
                Debug.Log($"[Player] Correct bin! +{pts}");
            }
            else
            {
                // Lixeira errada → descarta: perde 20, quebra combo e LIBERA a mão
                // (pode ser usado como estratégia para se livrar de um lixo difícil).
                GameManager.Instance?.AddScore(-20);
                GameManager.Instance?.BreakCombo();
                Juice.Text(at, "-20  DESCARTADO", new Color(0.95f, 0.3f, 0.3f), 1f);
                Juice.Flash(binSr, new Color(0.9f, 0.3f, 0.3f), 0.15f);
                Juice.Burst(bin.transform.position + Vector3.up, new Color(0.6f, 0.6f, 0.6f), 8, 4f);
                Juice.Shake(0.22f, 0.22f);
                Juice.HitStop(0.05f);
                AudioManager.Instance?.PlayDeliverBad();

                heldTrash.transform.SetParent(null);
                heldTrash.Deliver();      // some de jogo (conta como resolvido) e libera a mão
                heldTrash = null;
            }
            return;
        }
        // Sem lixeira por perto → não faz nada (o lixo continua na mão).
    }

    // ── Accessors ─────────────────────────────────────────────────────────────

    public bool IsCarrying => heldTrash != null;
    public TrashItem.TrashType? HeldType => heldTrash?.trashType;
}
