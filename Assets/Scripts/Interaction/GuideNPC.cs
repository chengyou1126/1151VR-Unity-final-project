using UnityEngine;

/// <summary>Guide 引導人。按 A 可以重新聽提示。</summary>
public class GuideNPC : MonoBehaviour, IInteractable
{
    public string speakerName = "Guide";
    public string[] hintLines = { "找到另外半張照片，把它和你手上的拼起來。" };

    public string GetInteractionText() { return "與 Guide 對話"; }

    public void Interact()
    {
        UIManager.Instance.Say(speakerName, hintLines);
    }
}
