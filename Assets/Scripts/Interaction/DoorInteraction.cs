using System.Collections;
using UnityEngine;

/// <summary>鉸鏈門：按 A 開關。hinge 設為門的轉軸（沒設就用自己）。</summary>
public class DoorInteraction : MonoBehaviour, IInteractable
{
    public Transform hinge;
    public float openAngle = 100f;
    public bool locked;
    public string lockedMessage = "門鎖住了";

    bool open, moving;
    Quaternion closedRot, openRot;

    void Awake()
    {
        if (hinge == null) hinge = transform;
        closedRot = hinge.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    public string GetInteractionText()
    {
        if (moving) return null;
        return open ? "關門" : "開門";
    }

    public void Interact()
    {
        if (locked) { UIManager.Instance.Toast(lockedMessage, 2f); return; }
        StartCoroutine(Swing(!open));
    }

    IEnumerator Swing(bool toOpen)
    {
        moving = true;
        AudioManager.Play(Sfx.Door);
        Quaternion from = hinge.localRotation;
        Quaternion to = toOpen ? openRot : closedRot;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        hinge.localRotation = to;
        open = toOpen;
        moving = false;
    }
}
