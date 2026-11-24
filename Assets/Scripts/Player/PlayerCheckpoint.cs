using UnityEngine;

/// <summary>記錄關卡起始點，被抓後把玩家送回起始點，並給一小段無敵時間避免連續扣命。</summary>
public class PlayerCheckpoint : MonoBehaviour
{
    public static PlayerCheckpoint Instance { get; private set; }

    Vector3 startPosition;
    float startYaw;
    float invulnerableUntil;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        Instance = this;
        startPosition = transform.position;
        startYaw = transform.eulerAngles.y;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetStart(Vector3 position, float yaw)
    {
        startPosition = position;
        startYaw = yaw;
    }

    /// <summary>把玩家直接移到某處（不扣命、不改重生點）。例如隱藏房間長出來時，玩家剛好站在它的範圍內。</summary>
    public void Teleport(Vector3 position)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        if (cc != null) cc.enabled = true;
        var move = GetComponent<PlayerMovement>();
        if (move != null) move.StopImmediately();
    }

    public void Respawn(float protectSeconds = 3f)
    {
        var cc = GetComponent<CharacterController>();
        var move = GetComponent<PlayerMovement>();
        var look = GetComponent<PlayerLook>();

        if (cc != null) cc.enabled = false;
        transform.position = startPosition;
        if (look != null) look.SetRotation(startYaw); else transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
        if (cc != null) cc.enabled = true;
        if (move != null) move.StopImmediately();

        invulnerableUntil = Time.time + protectSeconds;
    }
}
