using System.Collections;
using UnityEngine;

/// <summary>
/// 隱藏密室：四塊碎片在正確視角拼成照片之後，照片裡的房間會「直接在實驗室裡長出來」，玩家從門走進去。
/// 房間的所有內容（牆、地板、控制電腦、燈…）都放在 <see cref="body"/> 底下，平常是關閉的。
/// 這個元件掛在一直開著的錨點上；錨點的 +Z 方向 = 從門口走進房間的方向，原點 = 門口中心（地面）。
/// </summary>
public class HiddenRoom : MonoBehaviour
{
    public GameObject body;                       // 房間本體（平常關閉）
    public SecretDoor door;                       // 入口門（走進去後關上，解除自毀後再打開）
    public ControlPanelInteraction console;       // 中央控制電腦
    public SelfDestructController selfDestruct;   // 牆上的自毀顯示器與警報燈
    public ProximityZone insideZone;              // 玩家走進房間裡面 → 第三關開始
    public ProximityZone exitZone;                // 房間門外 → 玩家離開密室，第四關開始
    public Transform mergePoint;                  // 四塊碎片飛向的位置（房間中央、半空中）
    public Transform exitSpot;                    // 門外的站位（第四關起始點 / 重生點）
    [Tooltip("房間的內部範圍（本地座標，+Z 往室內）：用來確認房間長出來時玩家不在裡面")]
    public Vector3 footprintMin = new Vector3(-5.5f, 0f, -0.4f);
    public Vector3 footprintMax = new Vector3(5.5f, 4f, 11f);
    public float growSeconds = 2.0f;

    public bool IsRevealed { get; private set; }

    /// <summary>房間長出來：從地面往上「長」到完整高度，伴隨開門聲。</summary>
    public IEnumerator RevealRoutine()
    {
        if (IsRevealed) yield break;
        IsRevealed = true;
        EvictPlayer();

        var t = body.transform;
        var full = t.localScale;
        t.localScale = new Vector3(full.x, 0.02f, full.z);
        body.SetActive(true);
        door.OpenInstant();
        if (selfDestruct != null) selfDestruct.Arm();   // 螢幕顯示 ARMED、警報燈閃爍
        AudioManager.Play(Sfx.Door);
        AudioManager.Play(Sfx.Success, 0.6f);
        for (float k = 0f; k < 1f; k += Time.deltaTime / growSeconds)
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            t.localScale = new Vector3(full.x, Mathf.Lerp(0.02f, 1f, s) * full.y, full.z);
            yield return null;
        }
        t.localScale = full;
    }

    /// <summary>直接出現（測試 / 工具用）。</summary>
    public void RevealInstant()
    {
        if (IsRevealed) return;
        IsRevealed = true;
        EvictPlayer();
        body.SetActive(true);
        door.OpenInstant();
        if (selfDestruct != null) selfDestruct.Arm();
    }

    /// <summary>玩家不能被關在房間的牆裡：如果剛好站在房間範圍內，先送到門外。</summary>
    public void ShowExterior()
    {
        // Shell 的直接子物件中，六片碰撞牆各自帶有實心牆材質。
        var shell = body.transform.Find("Shell");
        Material wallMaterial = null;
        foreach (Transform part in shell)
        {
            if (!part.TryGetComponent<BoxCollider>(out _) || !part.TryGetComponent<MeshRenderer>(out var wall)) continue;
            wall.enabled = true;
            wallMaterial = wall.sharedMaterial;
        }
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "ExteriorRoof";
        roof.transform.SetParent(shell, false);
        roof.transform.localPosition = new Vector3(0f, 4.13f, 5.25f);
        roof.transform.localScale = new Vector3(10.8f, 0.14f, 10.8f);
        if (wallMaterial != null) roof.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
    }

    void EvictPlayer()
    {
        var cp = PlayerCheckpoint.Instance;
        if (cp == null) return;
        Vector3 local = transform.InverseTransformPoint(cp.transform.position);
        if (local.x > footprintMin.x - 0.4f && local.x < footprintMax.x + 0.4f && local.z > footprintMin.z - 0.4f && local.z < footprintMax.z + 0.4f)
            cp.Teleport(exitSpot != null ? exitSpot.position : transform.position - transform.forward * 3f);
    }
}
