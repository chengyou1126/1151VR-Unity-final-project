/// <summary>可被 Raycast + A 鍵互動的物件。GetInteractionText 回傳 null/空字串代表目前不可互動。</summary>
public interface IInteractable
{
    string GetInteractionText();
    void Interact();
}
