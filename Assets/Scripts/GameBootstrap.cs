using UnityEngine;

// Builds the ENTIRE EcoDefender game scene from code at runtime, with a flat,
// high-contrast "neo-brutalist" look (bold colours, thick black outlines).
// Put a single GameObject with this component in an empty scene and press Play.
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    [Header("Layout")]
    public float groundY = -4.5f;
    public float playerY = -3.6f;
    public float playAreaMinX = -8f;
    public float playAreaMaxX =  8f;

    [Header("Tuning")]
    public float playerSpeed = 9f;
    public float trashSpawnY = 8f;     // above the visible top → trash "falls in"

    // Sorting layers (back → front)
    const int SKY = -20, SUN = -15, BUILD_OUT = -13, BUILD = -12, WIN = -11,
              CLOUDS = -10, GROUND = 0, BIN = 1, PLAYER = 2, TRASH = 3;

    // Palette
    static readonly Color SkyCol   = new Color(0.55f, 0.82f, 0.98f);
    static readonly Color SoilCol  = new Color(0.55f, 0.36f, 0.20f);
    static readonly Color GrassCol = new Color(0.40f, 0.78f, 0.30f);
    static readonly Color Black    = Color.black;

    void Awake()
    {
        BuildCamera();
        BuildBackground();
        BuildGround();
        BuildBins();
        Player player = BuildPlayer();
        BuildManagers(player);
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    void BuildCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyCol;
    }

    // ── Background ──────────────────────────────────────────────────────────────

    void BuildBackground()
    {
        // Flat sky
        var sky = Sprite(null, "Sky", GameAssets.Square, SkyCol, new Vector3(0, 0, 0), SKY);
        sky.transform.localScale = new Vector3(28, 14, 1);

        // Sun (Circle already has a black outline)
        Sprite(null, "Sun", GameAssets.Circle, new Color(1f, 0.85f, 0.2f),
            new Vector3(-7.3f, 4.3f, 0), SUN).transform.localScale = Vector3.one * 1.8f;

        // Skyline — flat blocks with thick black outline + bright windows
        float[] bx = { -8.5f, -6f, -3.5f, -1f, 1.5f, 4f, 6.5f, 8.5f };
        float[] bh = { 4.5f, 6.2f, 3.5f, 5.5f, 4f, 6.6f, 3.8f, 5f };
        var buildCol = new Color(0.32f, 0.40f, 0.55f);
        for (int i = 0; i < bx.Length; i++)
        {
            var pos = new Vector3(bx[i], groundY + bh[i] / 2f - 0.5f, 0);
            // black outline (slightly larger square behind)
            Sprite(null, "BuildOutline", GameAssets.Square, Black, pos, BUILD_OUT)
                .transform.localScale = new Vector3(2.1f + 0.25f, bh[i] + 0.25f, 1);
            Sprite(null, "Building", GameAssets.Square, buildCol, pos, BUILD)
                .transform.localScale = new Vector3(2.1f, bh[i], 1);
            for (float wy = 1; wy < bh[i] - 0.5f; wy += 1.2f)
                Sprite(null, "Win", GameAssets.Square, new Color(1f, 0.92f, 0.5f),
                    new Vector3(bx[i], groundY + wy - 0.5f, 0), WIN)
                    .transform.localScale = new Vector3(1.3f, 0.3f, 1);
        }

        MakeCloud(new Vector3(-3.5f, 3.6f, 0), 1f);
        MakeCloud(new Vector3(3.0f, 4.6f, 0), 1.3f);
        MakeCloud(new Vector3(6.6f, 3.1f, 0), 0.9f);
    }

    void MakeCloud(Vector3 pos, float scale)
    {
        var c = Color.white;
        Sprite(null, "Cloud", GameAssets.Circle, c, pos, CLOUDS).transform.localScale = Vector3.one * 1.5f * scale;
        Sprite(null, "Cloud", GameAssets.Circle, c, pos + Vector3.right * 0.9f * scale, CLOUDS).transform.localScale = Vector3.one * 1.1f * scale;
        Sprite(null, "Cloud", GameAssets.Circle, c, pos + Vector3.left * 0.9f * scale, CLOUDS).transform.localScale = Vector3.one * 1.1f * scale;
    }

    // ── Ground ──────────────────────────────────────────────────────────────

    void BuildGround()
    {
        var soil = new GameObject("Ground");
        soil.transform.position = new Vector3(0, groundY, 0);
        soil.transform.localScale = new Vector3(24, 1.6f, 1);
        var sr = soil.AddComponent<SpriteRenderer>();
        sr.sprite = GameAssets.Square;
        sr.color = SoilCol;
        sr.sortingOrder = GROUND;
        soil.AddComponent<BoxCollider2D>();
        soil.AddComponent<Ground>();

        // Grass strip + black top edge line
        Sprite(null, "Grass", GameAssets.Square, GrassCol,
            new Vector3(0, groundY + 0.75f, 0), GROUND + 1).transform.localScale = new Vector3(24, 0.5f, 1);
        Sprite(null, "GroundEdge", GameAssets.Square, Black,
            new Vector3(0, groundY + 1.0f, 0), GROUND + 2).transform.localScale = new Vector3(24, 0.12f, 1);
    }

    // ── Recycling bins (4 types) ──────────────────────────────────────────────

    void BuildBins()
    {
        var types = new[]
        {
            TrashItem.TrashType.Organic,
            TrashItem.TrashType.Plastic,
            TrashItem.TrashType.Paper,
            TrashItem.TrashType.Metal,
        };
        float[] xs = { -6f, -2f, 2f, 6f };

        for (int i = 0; i < types.Length; i++)
        {
            Color col = TrashItem.ColorFor(types[i]);

            var parent = new GameObject($"Bin_{types[i]}");
            parent.transform.position = new Vector3(xs[i], groundY + 1.2f, 0);

            var trigger = parent.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.6f, 2.2f);

            var bin = parent.AddComponent<RecyclingBin>();
            bin.AcceptedType = types[i];

            var body = GameAssets.MakeRoundedRect(52, 60, 10, col, Black, 5);
            Sprite(parent.transform, "Body", body, Color.white, Vector3.zero, BIN)
                .transform.localScale = Vector3.one * 1.4f;

            var lid = GameAssets.MakeRoundedRect(64, 18, 6, col * 0.7f, Black, 5);
            Sprite(parent.transform, "Lid", lid, Color.white, new Vector3(0, 0.8f, 0), BIN + 1)
                .transform.localScale = Vector3.one * 1.4f;

            // white recycle diamond
            var mark = Sprite(parent.transform, "Mark", GameAssets.Square, Color.white,
                new Vector3(0, -0.05f, 0), BIN + 1);
            mark.transform.localScale = Vector3.one * 0.35f;
            mark.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
    }

    // ── Player (little character) ──────────────────────────────────────────────

    Player BuildPlayer()
    {
        var go = new GameObject("Player");
        go.transform.position = new Vector3(0, playerY, 0);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.85f;

        var player = go.AddComponent<Player>();
        player.minX = playAreaMinX;
        player.maxX = playAreaMaxX;
        player.moveSpeed = playerSpeed;

        var body = GameAssets.MakeRoundedRect(44, 48, 12, new Color(0.20f, 0.65f, 0.95f), Black, 5);
        Sprite(go.transform, "Body", body, Color.white, new Vector3(0, -0.15f, 0), PLAYER)
            .transform.localScale = Vector3.one * 1.05f;

        Sprite(go.transform, "Head", GameAssets.Circle, new Color(1f, 0.83f, 0.65f),
            new Vector3(0, 0.6f, 0), PLAYER + 1).transform.localScale = Vector3.one * 0.7f;
        Sprite(go.transform, "Eye", GameAssets.Circle, Color.black,
            new Vector3(-0.14f, 0.63f, 0), PLAYER + 2).transform.localScale = Vector3.one * 0.14f;
        Sprite(go.transform, "Eye", GameAssets.Circle, Color.black,
            new Vector3(0.14f, 0.63f, 0), PLAYER + 2).transform.localScale = Vector3.one * 0.14f;

        var mark = Sprite(go.transform, "Mark", GameAssets.Square, Color.white,
            new Vector3(0, -0.1f, 0), PLAYER + 2);
        mark.transform.localScale = Vector3.one * 0.2f;
        mark.transform.localRotation = Quaternion.Euler(0, 0, 45);

        var hold = new GameObject("HoldPoint");
        hold.transform.SetParent(go.transform);
        hold.transform.localPosition = new Vector3(0, 1.2f, 0);
        player.holdPoint = hold.transform;

        return player;
    }

    // ── Managers / systems ────────────────────────────────────────────────────

    void BuildManagers(Player player)
    {
        var audioGo = new GameObject("AudioManager");
        audioGo.AddComponent<AudioManager>();

        var gmGo = new GameObject("GameManager");
        gmGo.AddComponent<GameManager>();

        var spGo = new GameObject("TrashSpawner");
        var spawner = spGo.AddComponent<TrashSpawner>();
        spawner.spawnY = trashSpawnY;
        spawner.spawnXMin = playAreaMinX + 1f;
        spawner.spawnXMax = playAreaMaxX - 1f;

        var depGo = new GameObject("DeploymentController");
        var dep = depGo.AddComponent<DeploymentController>();
        dep.minX = playAreaMinX;
        dep.maxX = playAreaMaxX;
        dep.minY = playerY + 0.5f;
        dep.maxY = 4f;

        var hudGo = new GameObject("HUD");
        hudGo.AddComponent<HUD>();
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    static GameObject Sprite(Transform parent, string name, Sprite sprite, Color color, Vector3 localPos, int order)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}
