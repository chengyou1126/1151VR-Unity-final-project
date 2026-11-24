using System;
using UnityEngine;

/// <summary>中央控制電腦：按 A 開啟組合語言解謎介面。</summary>
public class ControlPanelInteraction : MonoBehaviour, IInteractable
{
    public bool usable = true;
    public string text = "使用中央控制電腦";
    public event Action OnUse;

    public string GetInteractionText() { return usable ? text : null; }

    public void Interact()
    {
        if (usable) OnUse?.Invoke();
    }
}
