using UnityEngine;

/// <summary>Raycast + A 鍵互動。顯示「[A] 開門」之類的提示。</summary>
public class PlayerInteraction : MonoBehaviour
{
    public Transform rayOrigin;      // 攝影機
    public float range = 3.2f;

    IInteractable current;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.Mode != InputMode.Gameplay || gm.State != GameState.Playing)
        {
            current = null;
            if (UIManager.Instance != null && gm.Mode != InputMode.Gameplay) UIManager.Instance.SetPrompt(null);
            return;
        }

        current = null;
        string text = null;

        if (rayOrigin != null &&
            Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target != null)
            {
                text = target.GetInteractionText();
                if (!string.IsNullOrEmpty(text)) current = target;
            }
        }

        UIManager.Instance.SetPrompt(current != null ? "[" + GameInput.InteractLabel + "] " + text : null);

        if (current != null && GameInput.Interact.WasPressedThisFrame())
            current.Interact();
    }
}
