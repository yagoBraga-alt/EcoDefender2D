using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Deployment, Action, GameOver, Victory }
    public GameState CurrentState { get; private set; }

    [Header("Wave Settings")]
    public int totalWaves = 5;
    // A onda NÃO começa por tempo: o jogador inicia manualmente (ENTER/Espaço ou
    // o botão COMECAR ONDA) quando terminar de posicionar suas unidades.

    // ── Wave summary (shown between waves) ─────────────────────────────────────
    public int LastWaveCompleted { get; private set; }   // 0 = nenhuma concluída ainda
    public int LastWaveScore { get; private set; }       // pontos ganhos só na última onda
    private int scoreAtWaveStart;

    [Header("Score")]
    public int score = 0;

    [Header("Coins (economia)")]
    public int startingCoins = 30;   // reserva inicial p/ comprar 1 coletor na 1ª preparação
    public int coins = 0;
    public event System.Action<int> OnCoinsChanged;

    [Header("City Health (lose condition)")]
    public float maxCityHealth = 100f;      // city starts full ("Cidade 100%")
    public float cityHealth = 100f;         // reaches 0 → Game Over
    public float damagePerStick = 20f;      // each stuck trash hurts the city
    public float CityHealth01 => Mathf.Clamp01(cityHealth / maxCityHealth);

    private int currentWave = 0;

    // Events so UI / other scripts can react without tight coupling
    public event System.Action<GameState> OnStateChanged;
    public event System.Action<int> OnScoreChanged;
    public event System.Action<float> OnCityHealthChanged;
    public event System.Action<int> OnWaveChanged;

    // ── Combo / multiplier (progression) ───────────────────────────────────────
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public event System.Action<int> OnComboChanged;

    // Multiplier grows every 3 clean deliveries, capped at 5x.
    public int Multiplier => Mathf.Clamp(1 + Combo / 3, 1, 5);

    // Returns the multiplier to apply to this delivery.
    public int RegisterGoodDelivery()
    {
        Combo++;
        if (Combo > BestCombo) BestCombo = Combo;
        OnComboChanged?.Invoke(Combo);
        return Multiplier;
    }

    public void BreakCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        OnComboChanged?.Invoke(Combo);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;            // reset global state at scene start
        TrashItem.FallMultiplier = 1f;
        cityHealth = maxCityHealth;     // city always starts at full health
        coins = startingCoins;
    }

    void Start()
    {
        StartDeploymentPhase();
    }

    void Update()
    {
        // Preparação: o jogador decide quando começar a onda (ENTER ou botão).
        // (Espaço fica reservado para pegar/entregar lixo na fase de Ação.)
        if (CurrentState == GameState.Deployment &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            BeginWave();
    }

    // Inicia a onda manualmente (tecla ou botão da HUD).
    public void BeginWave()
    {
        if (CurrentState == GameState.Deployment)
            StartActionPhase();
    }

    // ── State transitions ────────────────────────────────────────────────────

    public void StartDeploymentPhase()
    {
        currentWave++;
        SetState(GameState.Deployment);
        OnWaveChanged?.Invoke(currentWave);
    }

    public void StartActionPhase()
    {
        scoreAtWaveStart = score;          // baseline p/ calcular os pontos da onda
        SetState(GameState.Action);
        TrashSpawner.Instance?.StartWave(currentWave);
    }

    // Called by TrashSpawner when a wave's trash is all resolved
    public void OnWaveComplete()
    {
        LastWaveCompleted = currentWave;
        LastWaveScore = score - scoreAtWaveStart;

        if (currentWave >= totalWaves)
            SetState(GameState.Victory);
        else
            StartDeploymentPhase();
    }

    // ── Score / Lives ────────────────────────────────────────────────────────

    public void AddScore(int points)
    {
        score += points;
        OnScoreChanged?.Invoke(score);
    }

    // ── Coins ──────────────────────────────────────────────────────────────────
    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        coins = Mathf.Max(0, coins + amount);
        OnCoinsChanged?.Invoke(coins);
    }

    // Tenta gastar; retorna false (sem debitar) se não houver moedas suficientes.
    public bool TrySpendCoins(int cost)
    {
        if (coins < cost) return false;
        coins -= cost;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }

    // Called when a trash sticks to the ground — the city loses health.
    public void DamageCity() => DamageCity(damagePerStick);
    public void DamageCity(float amount)
    {
        if (CurrentState == GameState.GameOver || CurrentState == GameState.Victory) return;
        cityHealth = Mathf.Max(0f, cityHealth - amount);
        OnCityHealthChanged?.Invoke(cityHealth);
        BreakCombo();
        HUD.Flash(new Color(0.95f, 0.2f, 0.2f, 0.45f));
        Juice.Shake(0.35f, 0.3f);
        if (cityHealth <= 0f)
            SetState(GameState.GameOver);
    }

    // ── Power-up effects ───────────────────────────────────────────────────────
    // Restores city health (recycling helpers / "Scrub" power-up).
    public void HealCity(float amount)
    {
        cityHealth = Mathf.Min(maxCityHealth, cityHealth + amount);
        OnCityHealthChanged?.Invoke(cityHealth);
    }

    public void SlowTrash(float seconds, float mult) => StartCoroutine(SlowTrashCo(seconds, mult));
    System.Collections.IEnumerator SlowTrashCo(float seconds, float mult)
    {
        TrashItem.FallMultiplier = mult;
        yield return new WaitForSeconds(seconds);
        TrashItem.FallMultiplier = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;   // safety: never carry slow-mo / hit-stop into the new scene
        TrashItem.FallMultiplier = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.Action:
                AudioManager.Instance?.PlayWaveStart();
                Juice.ZoomPunch(0.06f);
                break;
            case GameState.Victory:
                AudioManager.Instance?.PlayVictory();
                HUD.Flash(new Color(0.3f, 0.9f, 0.4f, 0.5f));
                Juice.SlowMo(0.35f, 0.9f);
                Juice.Shake(0.2f, 0.4f);
                break;
            case GameState.GameOver:
                AudioManager.Instance?.PlayGameOver();
                HUD.Flash(new Color(0.9f, 0.15f, 0.15f, 0.55f));
                Juice.Shake(0.5f, 0.5f);
                break;
        }

        Debug.Log($"[GameManager] State → {newState}  Wave {currentWave}");
    }

    public int GetCurrentWave() => currentWave;
}
