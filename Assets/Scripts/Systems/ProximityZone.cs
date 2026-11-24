using System;
using UnityEngine;

/// <summary>
/// 區域觸發（穿越終點、逃脫點、密室入口…）。
/// 用 BoxCollider 的範圍做位置判斷，不依賴 Rigidbody / OnTriggerEnter，
/// 所以搭配 CharacterController 也一定會觸發。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ProximityZone : MonoBehaviour
{
    public bool zoneActive = true;
    public bool fireOnce = true;
    public event Action OnPlayerEnter;

    BoxCollider box;
    bool inside;
    bool fired;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    void Update()
    {
        if (!zoneActive) { inside = false; return; }
        var cp = PlayerCheckpoint.Instance;
        if (cp == null || GameManager.Instance.State != GameState.Playing) return;

        bool now = box.bounds.Contains(cp.transform.position + Vector3.up * 0.9f);
        if (now && !inside && !(fired && fireOnce))
        {
            fired = true;
            OnPlayerEnter?.Invoke();
        }
        inside = now;
    }
}
