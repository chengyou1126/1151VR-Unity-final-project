using UnityEngine;

/// <summary>抓捕判定：玩家進入抓捕距離 → 通知 LevelManager 扣命、重生。</summary>
[RequireComponent(typeof(GuardAI))]
public class GuardCatch : MonoBehaviour
{
    public float catchDistance = 1.3f;

    GuardAI ai;

    void Awake()
    {
        ai = GetComponent<GuardAI>();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!ai.CanCatch || gm.State != GameState.Playing || gm.Mode != InputMode.Gameplay) return;

        var player = PlayerCheckpoint.Instance;
        if (player == null || player.IsInvulnerable || LevelManager.Current == null) return;

        Vector3 d = player.transform.position - transform.position;
        float dy = Mathf.Abs(d.y);
        d.y = 0f;
        if (d.magnitude <= catchDistance && dy < 1.5f)
            LevelManager.Current.OnPlayerCaught(ai);
    }
}
