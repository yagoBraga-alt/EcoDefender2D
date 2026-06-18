using UnityEngine;

// Handles the Deployment phase: the player clicks on the map to place a
// limited number of Collectors before each wave begins. A translucent "ghost"
// follows the mouse so it's obvious what clicking will do.
public class DeploymentController : MonoBehaviour
{
    [Header("Deployment")]
    public int collectorsPerWave = 3;
    public float minX = -8f, maxX = 8f;   // where collectors may be placed
    public float minY = -3f, maxY = 4f;
    public float collectorRadius = 0.8f;  // small — collectors only cover a narrow column

    public int CollectorsRemaining { get; private set; }

    private Camera cam;
    private GameObject ghost;          // preview that follows the mouse
    private SpriteRenderer ghostBody;

    void Start()
    {
        cam = Camera.main;
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        CollectorsRemaining = collectorsPerWave;
        BuildGhost();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void BuildGhost()
    {
        ghost = new GameObject("CollectorGhost");
        ghostBody = ghost.AddComponent<SpriteRenderer>();
        ghostBody.sprite = GameAssets.Circle;
        ghostBody.color = new Color(0.1f, 0.85f, 0.85f, 0.45f);
        ghostBody.sortingOrder = 4;

        var ring = new GameObject("GhostRadius");
        ring.transform.SetParent(ghost.transform);
        ring.transform.localPosition = Vector3.zero;
        var ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = GameAssets.MakeRing(96, 6);
        ringSr.color = new Color(1f, 1f, 1f, 0.4f);
        ringSr.sortingOrder = 3;
        ring.transform.localScale = Vector3.one * (collectorRadius * 2f);

        ghost.SetActive(false);
    }

    void HandleStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Deployment)
            CollectorsRemaining = collectorsPerWave;
    }

    void Update()
    {
        bool deploying = GameManager.Instance?.CurrentState == GameManager.GameState.Deployment
                         && CollectorsRemaining > 0;

        ghost.SetActive(deploying);
        if (!deploying) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        world.x = Mathf.Clamp(world.x, minX, maxX);
        world.y = Mathf.Clamp(world.y, minY, maxY);
        ghost.transform.position = world;

        // green when valid, red-ish otherwise (always valid after clamp, but tint for feel)
        ghostBody.color = new Color(0.1f, 0.85f, 0.85f, 0.5f);

        if (Input.GetMouseButtonDown(0))
            PlaceCollector(world);
    }

    void PlaceCollector(Vector3 pos)
    {
        var go = new GameObject("Collector");
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();   // Collector.Start fills sprite/colour/ring
        var c = go.AddComponent<Collector>();
        c.radius = collectorRadius;

        AudioManager.Instance?.PlayPlace();
        CollectorsRemaining--;
    }
}
