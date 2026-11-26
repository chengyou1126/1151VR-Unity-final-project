using System.Collections;
using UnityEngine;

/// <summary>第一關：現代實驗室。Guide 教學 → 找另外半張照片 → 拼接 → 時空穿越。</summary>
public class ModernLabController : MonoBehaviour
{
    public GuideNPC guide;
    public Transform laboratory;
    public Material modernFinish;
    public Material restoredFinish;

    bool travelStarted;

    IEnumerator Start()
    {
        var lm = LevelManager.Current;
        var ui = UIManager.Instance;
        var gm = GameManager.Instance;
        if (gm.Data.returningToGuide)
        {
            yield return Epilogue(ui, gm);
            yield break;
        }
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
        ui.Toast("瞄準物件後，按 " + GameInput.InteractLabel + " 互動", 4f);
    }

    IEnumerator Epilogue(UIManager ui, GameManager gm)
    {
        ui.ResetTransient();
        ui.SetHudVisible(false);
        gm.SetMode(InputMode.Locked);
        RenderSettings.fog = false;
        foreach (var surface in laboratory.GetComponentsInChildren<Renderer>(true))
        {
            var finishes = surface.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < finishes.Length; i++)
            {
                if (finishes[i] != modernFinish) continue;
                finishes[i] = restoredFinish;
                changed = true;
            }
            if (changed) surface.sharedMaterials = finishes;
        }

        RenderSettings.ambientLight = new Color(0.58f, 0.71f, 0.88f);
        foreach (var lamp in FindObjectsByType<AlarmLight>(FindObjectsSortMode.None))
        {
            // 把現代實驗室的閃爍燈改成第二張地圖的穩定藍白光。
            lamp.flicker = false;
            lamp.color = new Color(0.6f, 0.82f, 1f);
            lamp.minIntensity = lamp.maxIntensity = 2.4f;
            lamp.speed = 0f;
            lamp.SetRunning(true);
        }
        foreach (var smoke in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var face = guide.transform;
        var player = PlayerCheckpoint.Instance;
        player.SetStart(face.position + face.forward * 2.8f, face.eulerAngles.y + 180f);
        player.Respawn(0f);
        var light = new GameObject("EpilogueLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.86f);
        light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(40f, face.eulerAngles.y + 180f, 0f);

        yield return ui.FadeRoutine(0f, 1f, Color.white);
        yield return ui.SayRoutine(guide.speakerName, new[]
        {
            "你回來了！你聽，警報停了。實驗室裡的每個人都平安。",
            "恭喜你阻止了這場事故。謝謝你，說好了在這裡等你，總算等到了。"
        }, 3.5f);
        gm.TriggerGameClear();
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
