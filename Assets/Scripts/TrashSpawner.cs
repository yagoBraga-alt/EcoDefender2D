using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashSpawner : MonoBehaviour
{
    public static TrashSpawner Instance { get; private set; }

    [Header("Prefabs — assign one per TrashType in Inspector")]
    public GameObject glassPrefab;
    public GameObject plasticPrefab;
    public GameObject paperPrefab;
    public GameObject metalPrefab;

    [Header("Spawn Area")]
    public float spawnY = 6f;          // height above screen where trash appears
    public float spawnXMin = -7f;
    public float spawnXMax =  7f;

    [Header("Wave Config")]
    public int baseTrashPerWave = 10;               // onda 1 = 10 lixos
    public int trashIncreasePerWave = 4;            // 10, 14, 18, 22, 26
    public float baseSpawnInterval = 1.8f;          // bem espaçado nas primeiras ondas
    public float intervalDecreasePerWave = 0.15f;   // caem mais juntos a cada onda
    public float minSpawnInterval = 0.7f;

    [Header("Fall Speed (acelera por onda)")]
    public float baseFallSpeed = 1.9f;               // 1.9, 2.5, 3.1, 3.7, 4.3
    public float fallSpeedIncreasePerWave = 0.6f;

    [Header("Power-ups & Events")]
    [Range(0f, 1f)] public float powerUpChancePerWave = 0.7f;
    [Range(0f, 1f)] public float eventChance = 0.5f;

    // Eventos especiais nomeados (PDF). RecycleWeek = tudo dourado;
    // os demais forçam um único tipo de resíduo a cair em massa.
    public enum WaveEvent { None, RecycleWeek, PlasticRain, MetalStorm, GlassInvasion }
    public WaveEvent CurrentEvent { get; private set; }

    // Tracking
    private int trashRemainingInWave;
    private int trashSpawnedInWave;
    private int totalTrashThisWave;
    private float currentFallSpeed;
    private bool goldenWave;                     // event: every item this wave is golden
    private TrashItem.TrashType? forcedType;     // event: only this type falls this wave

    // Contador exposto p/ a HUD ("lixos restantes até a próxima onda")
    public int TrashRemaining => Mathf.Max(0, trashRemainingInWave);
    public int TotalThisWave  => totalTrashThisWave;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Called by GameManager when the Action phase starts
    public void StartWave(int waveNumber)
    {
        totalTrashThisWave  = baseTrashPerWave + (waveNumber - 1) * trashIncreasePerWave;
        float interval = Mathf.Max(minSpawnInterval, baseSpawnInterval - (waveNumber - 1) * intervalDecreasePerWave);
        currentFallSpeed = baseFallSpeed + (waveNumber - 1) * fallSpeedIncreasePerWave;

        // ── Wave event ──────────────────────────────────────────────────────
        // Ondas 1-2: normais (introdução). Onda 3: chance de evento.
        // Onda 4: evento garantido. Onda 5+: evento de tipo garantido (mais difícil).
        goldenWave = false;
        forcedType = null;
        CurrentEvent = PickEvent(waveNumber);
        ApplyEvent(CurrentEvent, ref totalTrashThisWave, ref interval, waveNumber);

        trashSpawnedInWave  = 0;
        trashRemainingInWave = totalTrashThisWave;

        StartCoroutine(SpawnRoutine(totalTrashThisWave, interval));

        // ── Maybe drop one helper power-up partway through the wave ──────────
        if (Random.value < powerUpChancePerWave)
            StartCoroutine(DropPowerUp(Random.Range(2f, Mathf.Max(3f, interval * totalTrashThisWave * 0.6f))));

        Debug.Log($"[TrashSpawner] Wave {waveNumber}: {totalTrashThisWave} items, interval {interval:F2}s, event={CurrentEvent}");
    }

    // ── Event selection & application ──────────────────────────────────────────

    WaveEvent PickEvent(int wave)
    {
        if (wave >= 5) return RandomFrom(WaveEvent.PlasticRain, WaveEvent.MetalStorm, WaveEvent.GlassInvasion);
        if (wave == 4) return RandomFrom(WaveEvent.RecycleWeek, WaveEvent.PlasticRain, WaveEvent.MetalStorm, WaveEvent.GlassInvasion);
        if (wave >= 3 && Random.value < eventChance)
            return RandomFrom(WaveEvent.RecycleWeek, WaveEvent.PlasticRain, WaveEvent.MetalStorm, WaveEvent.GlassInvasion);
        return WaveEvent.None;
    }

    WaveEvent RandomFrom(params WaveEvent[] opts) => opts[Random.Range(0, opts.Length)];

    void ApplyEvent(WaveEvent ev, ref int total, ref float interval, int wave)
    {
        bool harder = wave >= 5;   // a "onda final" do PDF é mais intensa
        switch (ev)
        {
            case WaveEvent.RecycleWeek:
                goldenWave = true;
                Announce("SEMANA DA RECICLAGEM!  (tudo vale 3x)", new Color(1f, 0.85f, 0.15f));
                break;
            case WaveEvent.PlasticRain:
                forcedType = TrashItem.TrashType.Plastic;
                total = Mathf.RoundToInt(total * (harder ? 1.7f : 1.4f));
                Announce("CHUVA DE PLASTICO!", TrashItem.ColorFor(TrashItem.TrashType.Plastic));
                break;
            case WaveEvent.MetalStorm:
                forcedType = TrashItem.TrashType.Metal;
                interval *= (harder ? 0.55f : 0.70f);
                Announce("TEMPESTADE DE METAL!", TrashItem.ColorFor(TrashItem.TrashType.Metal));
                break;
            case WaveEvent.GlassInvasion:
                forcedType = TrashItem.TrashType.Glass;
                total = Mathf.RoundToInt(total * (harder ? 1.6f : 1.3f));
                Announce("INVASAO DE VIDROS!", TrashItem.ColorFor(TrashItem.TrashType.Glass));
                break;
        }
    }

    void Announce(string text, Color c)
    {
        Juice.Text(new Vector3(0f, 3.5f, 0f), text, c, 2.2f, 0.6f, 1.9f);
        AudioManager.Instance?.PlayWaveStart();
    }

    IEnumerator DropPowerUp(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Action) yield break;

        var go = new GameObject("PowerUp");
        go.transform.position = new Vector3(Random.Range(spawnXMin, spawnXMax), spawnY, 0f);
        var pu = go.AddComponent<PowerUp>();
        pu.kind = (PowerUp.Kind)Random.Range(0, 3);
        pu.despawnY = -6f;
    }

    IEnumerator SpawnRoutine(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.Action)
                yield break;

            SpawnOneTrash();
            trashSpawnedInWave++;
            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnOneTrash()
    {
        // Eventos de tipo único forçam o resíduo; senão, sorteia normalmente.
        TrashItem.TrashType type = forcedType ?? (TrashItem.TrashType)Random.Range(0, 4);

        float x = Random.Range(spawnXMin, spawnXMax);
        Vector3 pos = new Vector3(x, spawnY, 0f);   // SEMPRE no topo (acima da tela)

        GameObject prefab = PrefabFor(type);
        GameObject obj = (prefab != null)
            ? Instantiate(prefab, pos, Quaternion.identity)
            : CreateTrash(type, pos);               // cria já na posição certa

        TrashItem item = obj.GetComponent<TrashItem>();
        if (item != null)
        {
            item.trashType = type;
            item.isGolden = goldenWave || Random.value < 0.10f;   // golden wave → all bonus
            item.fallSpeed = currentFallSpeed;                    // queda acelera por onda
        }
    }

    // Called by TrashItem when it is either delivered or stuck
    public void NotifyTrashResolved()
    {
        trashRemainingInWave--;
        if (trashRemainingInWave <= 0 && trashSpawnedInWave >= totalTrashThisWave)
            GameManager.Instance?.OnWaveComplete();
    }

    GameObject PrefabFor(TrashItem.TrashType type) => type switch
    {
        TrashItem.TrashType.Glass   => glassPrefab,
        TrashItem.TrashType.Plastic => plasticPrefab,
        TrashItem.TrashType.Paper   => paperPrefab,
        TrashItem.TrashType.Metal   => metalPrefab,
        _ => null
    };

    // Cria o lixo JÁ na posição de spawn (sem arte importada). Importante: NÃO
    // usar um "molde" em (0,0,0) + Instantiate — o molde ficaria vivo no meio da
    // tela como um lixo extra. Aqui o objeto nasce direto em 'pos' (topo).
    GameObject CreateTrash(TrashItem.TrashType type, Vector3 pos)
    {
        GameObject go = new GameObject($"Trash_{type}");
        go.tag = "Trash";
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GameAssets.TrashSprite(type);
        sr.color  = TrashItem.ColorFor(type);
        sr.sortingOrder = 3;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one * 0.6f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        go.AddComponent<TrashItem>();
        return go;
    }
}
