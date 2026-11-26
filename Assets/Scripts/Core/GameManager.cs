using System;
using UnityEngine;

public enum GameState { MainMenu, Playing, Paused, GameOver, GameClear }
public enum LifeLossReason { Caught, WrongAnswer }

/// <summary>Gameplay=玩家可操作；UI=選單/介面；Puzzle=照片拼圖；Locked=過場動畫（無輸入，時間照常）</summary>
public enum InputMode { Gameplay, UI, Puzzle, Locked }

/// <summary>跨關卡保留的整場遊戲資料。只有「新遊戲」或「Game Over 後重新開始」才會重設。</summary>
[Serializable]
public class RunData
{
    public bool returningToGuide;
    public int lives = GameManager.MaxLives;
    public int stage = 1;                         // 1~4
    public bool[] photoPieces = new bool[4];      // 第一關用 [0..1]，第二關用 [0..3]
    public bool photoPuzzleSolved;        // 第一關：兩半拼好 / 第二關：四塊碎片已對齊（照片之門開啟）
    public bool[] framesPlaced = new bool[4];   // 第二關：四個相框各自是否已放入碎片
    public bool portalOpen;               // 照片之門是否已開啟（第四關為 true）
    public int quizStep;                          // 第三關進度（AssemblyQuestions.Steps 的索引）
    public bool selfDestructDisarmed;

    public int PieceCount
    {
        get { int n = 0; foreach (var p in photoPieces) if (p) n++; return n; }
    }
}

public class GameManager : MonoBehaviour
{
    public const int MaxLives = 5;

    public static GameManager Instance { get; private set; }

    public RunData Data { get; private set; }
    public GameState State { get; private set; } = GameState.MainMenu;
    public InputMode Mode { get; private set; } = InputMode.UI;

    public event Action<int> OnLivesChanged;
    public event Action<GameState> OnStateChanged;
    public event Action<LifeLossReason> OnLifeLost;
    public event Action<InputMode> OnModeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Data = NewRunData();
        var _ = GameInput.Asset; // 建立輸入資產
        SetMode(InputMode.UI);
    }

    static RunData NewRunData()
    {
        var d = new RunData();
        d.photoPieces[0] = true; // 玩家初始持有半張照片
        return d;
    }

    // ───────────── 遊戲流程 ─────────────

    public void StartNewGame()
    {
        Data = NewRunData();
        OnLivesChanged?.Invoke(Data.lives);
        SetState(GameState.Playing);
        SceneLoader.Load(SceneNameForStage(1));
    }

    public void ReturnToMainMenu()
    {
        Data = NewRunData();
        OnLivesChanged?.Invoke(Data.lives);
        SetState(GameState.MainMenu);
        SceneLoader.Load("MainMenu");
    }

    public void AdvanceToStage(int stage, Color? fadeColor = null)
    {
        Data.stage = stage;
        if (stage == 2)
        {
            Data.photoPieces = new bool[4];
            Data.photoPuzzleSolved = false;
            Data.framesPlaced = new bool[4];
            Data.portalOpen = false;
        }
        SceneLoader.Load(SceneNameForStage(stage), fadeColor);
    }

    public static string SceneNameForStage(int stage)
    {
        switch (stage)
        {
            case 1: return "ModernLab";
            default: return "PastLab";   // 第二、三、四關都在過去實驗室裡（隱藏密室會在實驗室中長出來）
        }
    }

    /// <summary>LevelManager 進入場景時呼叫，回傳目前關卡（1~4；主選單為 0）。</summary>
    public int EnterScene(SceneKind kind)
    {
        switch (kind)
        {
            case SceneKind.MainMenu:
                SetState(GameState.MainMenu);
                return 0;
            case SceneKind.ModernLab:
                if (!Data.returningToGuide) Data.stage = 1;
                break;
            case SceneKind.PastLab: Data.stage = 2; break;   // 進場一定從第二關開始；三、四關由 PastLabController 在場景內推進
        }
        if (State == GameState.MainMenu) SetState(GameState.Playing); // 直接從編輯器開某個場景測試
        return Data.stage;
    }

    // ───────────── 生命系統 ─────────────

    /// <returns>true=仍有生命；false=生命歸零（已觸發 Game Over）</returns>
    public bool LoseLife(LifeLossReason reason)
    {
        if (State != GameState.Playing) return Data.lives > 0;
        if (Data.lives <= 0) return false;

        Data.lives = Mathf.Max(0, Data.lives - 1);
        OnLivesChanged?.Invoke(Data.lives);
        OnLifeLost?.Invoke(reason);

        if (Data.lives <= 0)
        {
            TriggerGameOver();
            return false;
        }
        return true;
    }

    void TriggerGameOver()
    {
        SetState(GameState.GameOver);
        SetMode(InputMode.UI);
        AudioManager.Play(Sfx.GameOver);
    }

    public void TriggerGameClear()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.GameClear);
        SetMode(InputMode.UI);
        AudioManager.Play(Sfx.GameClear);
    }

    // ───────────── 暫停 / 輸入模式 ─────────────

    public void Pause()
    {
        if (State != GameState.Playing || Mode != InputMode.Gameplay) return;
        SetState(GameState.Paused);
        SetMode(InputMode.UI);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        SetState(GameState.Playing);
        SetMode(InputMode.Gameplay);
    }

    void SetState(GameState s)
    {
        if (State == s) return;
        State = s;
        OnStateChanged?.Invoke(s);
    }

    public void SetMode(InputMode mode)
    {
        Mode = mode;
        GameInput.SetMode(mode);
        Time.timeScale = (mode == InputMode.Gameplay || mode == InputMode.Locked) ? 1f : 0f;
        bool free = mode == InputMode.UI || mode == InputMode.Puzzle;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
        OnModeChanged?.Invoke(mode);
    }
}
