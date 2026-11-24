using UnityEngine;

/// <summary>
/// 照片 / 照片碎片。每張有獨立 ID，只能拾取一次。
/// 第一關：另外半張照片 pieceId = 1（0 是玩家初始持有的那半張）。
/// 第二關：四張碎片 pieceId = 0~3。
/// </summary>
public class PhotoPickup : MonoBehaviour, IInteractable
{
    public int pieceId;
    public string displayName = "照片碎片";

    public bool Collected => GameManager.Instance.Data.photoPieces[pieceId];

    void Start()
    {
        // 已經拾取過（例如被抓後重生）就不再出現
        if (Collected) gameObject.SetActive(false);
    }

    public string GetInteractionText()
    {
        return Collected ? null : "拾取 " + displayName;
    }

    public void Interact()
    {
        if (Collected) return;
        AudioManager.Play(Sfx.Pickup);
        PhotoPuzzleManager.Instance.Collect(pieceId);
        gameObject.SetActive(false);
    }
}
