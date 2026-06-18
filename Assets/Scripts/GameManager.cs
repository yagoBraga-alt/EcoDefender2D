using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Deployment, Action, GameOver, Victory }
    public GameState CurrentState { get; private set; }

    [Header("Wave Settings")]
    public int totalWaves = 3;
    public float deploymentDuration = 10f; // seconds player has to place collectors

    [Header("Score")]
    public int score = 0;
    public int lives = 3;

    private int currentWave = 0;
    private float deploymentTimer;

    // Events so UI / other scripts can react without tight coupling
    public event System.Action<GameState> OnStateChanged;
    public event System.Action<int> OnScoreChanged;
    public event System.Action<int> OnLivesChanged;
    public event System.Action<int> OnWaveChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        StartDeploymentPhase();
    }

    void Update()
    {
        if (CurrentState == GameState.Deployment)
        {
            deploymentTimer -= Time.deltaTime;
            if (deploymentTimer <= 0f)
                StartActionPhase();
        }
    }

    // ── State transitions ────────────────────────────────────────────────────

    public void StartDeploymentPhase()
    {
        currentWave++;
        deploymentTimer = deploymentDuration;
        SetState(GameState.Deployment);
        OnWaveChanged?.Invoke(currentWave);
    }

    public void StartActionPhase()
    {
        SetState(GameState.Action);
        TrashSpawner.Instance?.StartWave(currentWave);
    }

    // Called by TrashSpawner when a wave's trash is all resolved
    public void OnWaveComplete()
    {
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

    public void LoseLife()
    {
        lives--;
        OnLivesChanged?.Invoke(lives);
        if (lives <= 0)
            SetState(GameState.GameOver);
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] State → {newState}  Wave {currentWave}");
    }

    public float GetDeploymentTimeRemaining() => deploymentTimer;
    public int GetCurrentWave() => currentWave;
}
