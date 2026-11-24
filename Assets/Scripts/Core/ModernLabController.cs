using System.Collections;
using UnityEngine;

/// <summary>第一關：現代實驗室。Guide 教學 → 找另外半張照片 → 拼接 → 時空穿越。</summary>
public class ModernLabController : MonoBehaviour
{
    public GuideNPC guide;

    bool travelStarted;

    IEnumerator Start()
    {
        var lm = LevelManager.Current;
        var ui = UIManager.Instance;
        var gm = GameManager.Instance;
        var pm = PhotoPuzzleManager.Instance;

        ui.SetMission("探索實驗室，尋找另外半張照片");
        ui.SetPhotoProgress(pm.CollectedCount, pm.TotalPieces);

        pm.OnPieceCollected += _ =>
        {
            ui.SetPhotoProgress(pm.CollectedCount, pm.TotalPieces);
            ui.SetMission("拼接兩張照片");
        };
        pm.OnAllCollected += () => StartCoroutine(AutoPuzzle(pm));
        pm.OnSolved += () => StartCoroutine(TravelRoutine());

        yield return lm.BeginLevel(giveControl: false);

        // Guide 開場白
        yield return ui.SayRoutine("Guide", new[]
        {
            "你終於醒了。實驗室發生了爆炸，危險的病毒已經洩漏出去。",
            "唯一的辦法，是利用這張照片的力量回到爆炸之前，查出事故的原因。",
            "但你手上只有半張照片。另一半應該還留在這間實驗室裡，找到它。",
            "找到之後把兩半拼起來，就能穿越時空。我會在這裡等你。"
        });

        // 操作教學
        bool helpClosed = false;
        ui.ShowControlsHelp(() => helpClosed = true);
        while (!helpClosed) yield return null;

        if (gm.State == GameState.Playing && gm.Mode != InputMode.Gameplay) gm.SetMode(InputMode.Gameplay);
        ui.Toast("用右類比搖桿瞄準物件，按 " + GameInput.InteractLabel + " 互動", 4f);
    }

    IEnumerator AutoPuzzle(PhotoPuzzleManager pm)
    {
        var gm = GameManager.Instance;
        yield return new WaitForSeconds(0.6f);
        while (gm.Mode != InputMode.Gameplay) yield return null;
        UIManager.Instance.Toast("兩張照片都到手了——把它們拼起來", 2.5f);
        yield return new WaitForSeconds(1.2f);
        while (gm.Mode != InputMode.Gameplay) yield return null;
        pm.OpenPuzzle();
    }

    IEnumerator TravelRoutine()
    {
        if (travelStarted) yield break;
        travelStarted = true;
        yield return new WaitForSecondsRealtime(0.3f);
        UIManager.Instance.SetMission("");
        TimeTravelEffect.Play(() => GameManager.Instance.AdvanceToStage(2, Color.white));
    }
}
