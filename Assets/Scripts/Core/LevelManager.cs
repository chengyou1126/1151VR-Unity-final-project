using System.Collections;
using UnityEngine;

public enum SceneKind { MainMenu, ModernLab, PastLab }

/// <summary>
/// 每個場景一個。負責：判斷目前是第幾關、放置玩家、淡入、被抓後的重生流程。
/// 各關的專屬流程在 ModernLabController / PastLabController
/// （PastLab 場景裡依序進行第二、三、四關：隱藏密室是第二關解開後直接在實驗室裡長出來的房間，第三、四關不換場景）。
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Current { get; private set; }

    public SceneKind kind;
    public Transform playerStart;        // 第一、二、三關起始點（PastLab 也是時空穿越位置）
    public Transform playerStartStage4;  // 第四關起始點（只有 PastLab 用）
    public GuardAI[] guards;

    public int Stage { get; private set; }
    bool caughtBusy;

    void Awake()
    {
        Current = this;
        Stage = GameManager.Instance.EnterScene(kind);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        if (kind == SceneKind.MainMenu)
        {
            UIManager.Instance.ResetTransient();
            UIManager.Instance.SetHudVisible(false);
            UIManager.Instance.ShowMainMenu();
            StartCoroutine(UIManager.Instance.FadeRoutine(0f, 0.6f, Color.black));
        }
    }

    /// <summary>在同一個場景裡進入下一關（PastLab：第二 → 三 → 四關）。同步更新整場遊戲資料與 HUD 的章節。</summary>
    public void SetStage(int stage)
    {
        Stage = stage;
        GameManager.Instance.Data.stage = stage;
        UIManager.Instance.SetChapter(stage);
    }

    Transform CurrentStart => (Stage == 4 && playerStartStage4 != null) ? playerStartStage4 : playerStart;

    /// <summary>各關控制器在 Start 的協程開頭呼叫：放置玩家、設定 HUD、淡入、交還操作。</summary>
    public IEnumerator BeginLevel(bool giveControl = true, Color? fadeColor = null)
    {
        var ui = UIManager.Instance;
        var gm = GameManager.Instance;
        ui.ResetTransient();

        var cp = PlayerCheckpoint.Instance;
        if (cp != null && CurrentStart != null)
        {
            cp.SetStart(CurrentStart.position, CurrentStart.eulerAngles.y);
            cp.Respawn(0f);
        }

        ui.SetHudVisible(true);
        ui.SetChapter(Stage);
        ui.RefreshLives();
        gm.SetMode(InputMode.Locked);
        yield return ui.FadeRoutine(0f, fadeColor.HasValue ? 1.6f : 0.8f, fadeColor ?? Color.black);
        if (giveControl) gm.SetMode(InputMode.Gameplay);
    }

    // ───────────── 被守衛抓到 ─────────────

    public void OnPlayerCaught(GuardAI by)
    {
        if (caughtBusy) return;
        StartCoroutine(CaughtRoutine());
    }

    IEnumerator CaughtRoutine()
    {
        caughtBusy = true;
        var gm = GameManager.Instance;
        var ui = UIManager.Instance;

        AudioManager.Play(Sfx.Caught);
        ui.Flash(new Color(1f, 0.05f, 0.05f, 0.6f), 0.9f);
        ui.Toast("被守衛抓到了！ 生命 -1", 2.2f);

        bool alive = gm.LoseLife(LifeLossReason.Caught);
        if (!alive) { caughtBusy = false; yield break; } // Game Over 由 GameManager 處理

        gm.SetMode(InputMode.Locked);
        yield return ui.FadeRoutine(1f, 0.35f, Color.black);
        RespawnPlayer();
        ResetGuards();
        yield return ui.FadeRoutine(0f, 0.5f, Color.black);
        gm.SetMode(InputMode.Gameplay);
        caughtBusy = false;
    }

    public void RespawnPlayer()
    {
        var cp = PlayerCheckpoint.Instance;
        if (cp != null) cp.Respawn(3f);
    }

    public void ResetGuards()
    {
        if (guards == null) return;
        foreach (var g in guards)
            if (g != null && g.gameObject.activeInHierarchy) g.ResetToStart();
    }
}
