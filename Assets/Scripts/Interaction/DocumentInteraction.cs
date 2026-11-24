using UnityEngine;

/// <summary>研究文件 / 一般可閱讀物件。按 A 顯示文字。</summary>
public class DocumentInteraction : MonoBehaviour, IInteractable
{
    public string title = "研究文件";
    [TextArea(2, 8)] public string body = "……";

    public string GetInteractionText() { return "閱讀 " + title; }

    public void Interact()
    {
        UIManager.Instance.Say(title, new[] { body });
    }
}
