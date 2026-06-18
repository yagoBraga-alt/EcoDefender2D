using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashSpawner : MonoBehaviour
{
    public static TrashSpawner Instance { get; private set; }

    [Header("Prefabs — assign one per TrashType in Inspector")]
    public GameObject organicPrefab;
    public GameObject plasticPrefab;
    public GameObject paperPrefab;
    public GameObject metalPrefab;

    [Header("Spawn Area")]
    public float spawnY = 6f;          // height above screen where trash appears
    public float spawnXMin = -7f;
    public float spawnXMax =  7f;

    [Header("Wave Config")]
    public int baseTrashPerWave = 5;   // increases each wave
    public float baseSpawnInterval = 1.5f;
    public float intervalDecreasePerWave = 0.2f; // spawn faster each wave

    // Tracking
    private int trashRemainingInWave;
    private int trashSpawnedInWave;
    private int totalTrashThisWave;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Called by GameManager when the Action phase starts
    public void StartWave(int waveNumber)
    {
        totalTrashThisWave  = baseTrashPerWave + (waveNumber - 1) * 3;
        trashSpawnedInWave  = 0;
        trashRemainingInWave = totalTrashThisWave;

        float interval = Mathf.Max(0.4f, baseSpawnInterval - (waveNumber - 1) * intervalDecreasePerWave);
        StartCoroutine(SpawnRoutine(totalTrashThisWave, interval));
        Debug.Log($"[TrashSpawner] Wave {waveNumber}: {totalTrashThisWave} items, interval {interval:F2}s");
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
        TrashItem.TrashType type = (TrashItem.TrashType)Random.Range(0, 4);
        GameObject prefab = PrefabFor(type);
        if (prefab == null)
        {
            prefab = CreateFallbackPrefab(type);
        }

        float x = Random.Range(spawnXMin, spawnXMax);
        Vector3 pos = new Vector3(x, spawnY, 0f);
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);

        // Make sure the TrashItem component knows its type (in case prefab has it pre-set)
        TrashItem item = obj.GetComponent<TrashItem>();
        if (item != null) item.trashType = type;
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
        TrashItem.TrashType.Organic => organicPrefab,
        TrashItem.TrashType.Plastic => plasticPrefab,
        TrashItem.TrashType.Paper   => paperPrefab,
        TrashItem.TrashType.Metal   => metalPrefab,
        _ => null
    };

    // Runtime fallback: coloured square so the game works even without art assets
    GameObject CreateFallbackPrefab(TrashItem.TrashType type)
    {
        GameObject go = new GameObject($"Trash_{type}");
        go.tag = "Trash";

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
