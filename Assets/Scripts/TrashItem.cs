using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TrashItem : MonoBehaviour
{
    public enum TrashType { Glass, Plastic, Paper, Metal }

    [Header("Type")]
    public TrashType trashType;
    public bool isGolden;

    public static float FallMultiplier = 1f;

    [Header("Fall Settings")]
    public float fallSpeed = 2.5f;
    public float settleOffset = 1.0f;   // altura do centro do lixo quando assenta no chão (acima de GroundY)

    [Header("Sticky Settings")]
    // PDF: resíduo gruda após 5 segundos no chão
    public float timeBeforeStick = 5f;

    public bool IsCollected { get; private set; }
    public bool IsStuck     { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private bool isGrounded;
    private float groundTimer;
    private Color originalColor;
    private int stickyStage = 0;   // 0=caindo  1=fresco  2=piscando  3=tóxico

    static readonly Color StickyBrown = new Color(0.35f, 0.18f, 0.02f);
    static readonly Color WarnOrange  = new Color(0.95f, 0.50f, 0.05f);

    // ── Cor padrão por tipo (padrão CONAMA BR) ────────────────────────────────
    public static Color ColorFor(TrashType t) => t switch
    {
        TrashType.Glass   => new Color(0.20f, 0.85f, 0.40f), // verde  (CONAMA)
        TrashType.Plastic => new Color(0.95f, 0.25f, 0.30f), // vermelho
        TrashType.Paper   => new Color(0.20f, 0.55f, 1.00f), // azul
        TrashType.Metal   => new Color(1.00f, 0.80f, 0.10f), // amarelo
        _ => Color.white
    };

    // Moedas por reciclagem correta (valores do PDF Trabalho 03).
    public static int CoinsFor(TrashType t) => t switch
    {
        TrashType.Paper   => 5,
        TrashType.Plastic => 10,
        TrashType.Glass   => 15,
        TrashType.Metal   => 20,
        _ => 5
    };

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // Queda controlada por posição (não por física): Kinematic evita que os
        // resíduos empilhem/quiquem uns nos outros — cada um assenta no chão e
        // "envelhece" até grudar. O Player/Coletores ainda detectam por trigger.
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        originalColor = ColorFor(trashType);
        if (sr) sr.color = originalColor;

        Juice.Pop(transform, 0.35f, 0.25f);
        Juice.Burst(transform.position, originalColor, 4, 2f, 0.1f, 0.3f);

        if (isGolden) AddGoldenMarker();
    }

    void AddGoldenMarker()
    {
        var ring = new GameObject("GoldRing");
        ring.transform.SetParent(transform);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = Vector3.one * 1.4f;
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = GameAssets.MakeRing(64, 8);
        rs.color = new Color(1f, 0.85f, 0.15f);
        rs.sortingOrder = (sr ? sr.sortingOrder : 3) - 1;
        ring.AddComponent<Pulse>().amount = 0.12f;
        ring.AddComponent<Twinkle>().speed = 6f;
    }

    void Update()
    {
        if (IsCollected || IsStuck) return;

        // Pausa total fora da fase de Ação: nada cai nem "envelhece" durante a
        // Preparação (o jogador está parado — nada deve acumular) / fim de jogo.
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.GameState.Action)
            return;

        float floorY = GameBootstrap.GroundY + settleOffset;

        if (!isGrounded)
        {
            transform.position += Vector3.down * fallSpeed * FallMultiplier * Time.deltaTime;
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);

            if (transform.position.y <= floorY)
            {
                var p = transform.position; p.y = floorY; transform.position = p;
                transform.rotation = Quaternion.identity;
                isGrounded = true;
            }
        }
        else
        {
            groundTimer += Time.deltaTime;
            CheckStickyStage();

            if (groundTimer >= timeBeforeStick)
                Stick();
        }
    }

    // ── Progressão dos 4 estágios visuais do Sticky ───────────────────────────
    //
    //  Estágio 1  (0 – 40% do tempo)   : recém-caído, escala normal
    //  Estágio 2  (40 – 70%)           : pisca laranja + pulsa (DOTween yoyo)
    //  Estágio 3  (70 – 100%)          : vira marrom/tóxico, achata
    //  Estágio 4  (100%)               : gruda → mancha no chão → polui cidade
    void CheckStickyStage()
    {
        float t = groundTimer / timeBeforeStick;

        if (t >= 0.40f && stickyStage < 2)
        {
            // Estágio 2: pisca laranja + pulsa de escala
            stickyStage = 2;
            DOTween.Kill(gameObject);

            if (sr)
            {
                sr.color = originalColor;
                TweenSpriteColor(sr, WarnOrange, 0.18f)
                  .SetLoops(-1, LoopType.Yoyo)
                  .SetEase(Ease.InOutQuad)
                  .SetId(gameObject);
            }
            transform.DOScale(Vector3.one * 1.22f, 0.18f)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetEase(Ease.InOutSine)
                     .SetId(gameObject);
        }
        else if (t >= 0.70f && stickyStage < 3)
        {
            // Estágio 3: vira marrom e achata visualmente
            stickyStage = 3;
            DOTween.Kill(gameObject);

            if (sr) TweenSpriteColor(sr, StickyBrown, 0.28f).SetId(gameObject);
            transform.DOScale(new Vector3(1.35f, 0.80f, 1f), 0.22f)
                     .SetEase(Ease.OutBack)
                     .SetId(gameObject);
        }
    }

    // Chamado pelo Player ao encostar
    public void Collect()
    {
        if (IsStuck || IsCollected) return;
        IsCollected = true;
        DOTween.Kill(gameObject);

        // Restaura a aparência ORIGINAL: se o lixo estava envelhecendo (laranja
        // piscando / marrom / achatado), ele volta à cor e forma do seu tipo, para
        // o jogador enxergar em qual lixeira deve entregar.
        stickyStage = 0;
        groundTimer = 0f;
        isGrounded = false;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        if (sr) sr.color = originalColor;

        gameObject.SetActive(false);
    }

    // Estágio 4: gruda ao chão
    void Stick()
    {
        IsStuck = true;
        DOTween.Kill(gameObject);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        if (sr) sr.color = StickyBrown;
        transform.localScale = new Vector3(1.4f, 0.6f, 1f);

        SpawnToxicStain();

        Juice.Burst(transform.position, StickyBrown, 12, 4f, 0.16f, 0.55f);
        Juice.Burst(transform.position, new Color(0.1f, 0.55f, 0.05f), 6, 3f, 0.12f, 0.4f);
        Juice.Flash(sr, Color.white, 0.1f);
        Juice.HitStop(0.05f);

        GameManager.Instance?.DamageCity();
        TrashSpawner.Instance?.NotifyTrashResolved();
        AudioManager.Instance?.PlayStick();

        // Some rápido após causar o dano, para não poluir a leitura da tela.
        // Encolhe e some (a mancha tóxica no chão permanece como feedback).
        transform.DOScale(Vector3.zero, 0.4f).SetDelay(2.2f).SetEase(Ease.InBack).SetId(gameObject);
        Destroy(gameObject, 2.7f);
    }

    // Cria mancha tóxica no chão que expande e depois esmaece
    void SpawnToxicStain()
    {
        float stainY = GameBootstrap.GroundY + 0.88f;

        var stain = new GameObject("ToxicStain");
        stain.transform.position = new Vector3(transform.position.x, stainY, 0f);

        var stainSr = stain.AddComponent<SpriteRenderer>();
        stainSr.sprite = GameAssets.MakeFilledCircle(64);
        stainSr.color  = new Color(0.10f, 0.38f, 0.03f, 0.92f);
        stainSr.sortingOrder = 1;

        // Expande do zero até formato achatado (como uma poça)
        stain.transform.localScale = Vector3.zero;
        stain.transform.DOScale(new Vector3(3.2f, 0.38f, 1f), 0.5f)
             .SetEase(Ease.OutElastic);

        // Esmaece gradualmente (DOTween.To no alpha — núcleo, sem módulo de sprite)
        DOTween.To(() => stainSr.color.a,
                   a => { var c = stainSr.color; c.a = a; stainSr.color = c; },
                   0f, 4.2f)
               .SetDelay(0.6f)
               .SetEase(Ease.InQuad);

        Destroy(stain, 5.2f);
    }

    // Anima a cor de um SpriteRenderer usando o núcleo do DOTween (DOTween.To),
    // evitando os atalhos .DOColor/.DOFade que dependem do "Setup DOTween".
    static Tweener TweenSpriteColor(SpriteRenderer s, Color to, float dur)
        => DOTween.To(() => s.color, c => s.color = c, to, dur);

    // Chamado pelo Player após entrega correta na lixeira
    public void Deliver()
    {
        DOTween.Kill(gameObject);
        TrashSpawner.Instance?.NotifyTrashResolved();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        DOTween.Kill(gameObject);
    }
}
