using UnityEngine;

/// <summary>
/// 第一人稱移動：走、跑（RB）、跳（LB / Space）、蹲（按住 B / Ctrl）。
/// 蹲下時碰撞盒變矮、鏡頭降低、移動變慢，可以鑽過矮縫；頭上有東西時放開蹲鍵也不會站起來（避免卡進天花板）。
/// 蹲著時守衛比較難發現你（見 GuardAI）。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("速度")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.2f;
    public float crouchSpeed = 1.8f;
    public float acceleration = 12f;

    [Header("跳躍 / 重力")]
    public float gravity = -20f;
    public float jumpHeight = 1.1f;
    public float coyoteTime = 0.12f;     // 剛離開邊緣的短時間內還可以起跳
    public float jumpBuffer = 0.12f;     // 落地前稍早按下跳也算

    [Header("蹲下")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float cameraStandY = 1.65f;
    public float cameraCrouchY = 0.85f;
    public float crouchTransitionSpeed = 6f;

    [Header("其他")]
    public float stepLength = 2.1f;
    [Tooltip("可以直接走上去的階梯高度，要和導航網格烘焙時的 agentClimb（0.3）一致。不要設太大：CharacterController 每次移動前會先把膠囊往上抬這麼高，0.5 會讓頭頂到 2 公尺高的門楣而卡在門口")]
    public float stepHeight = 0.3f;

    // 測試 / 除錯用：由測試腳本直接餵輸入（本地座標：x=左右、y=前後）
    [System.NonSerialized] public Vector2? debugInput;
    [System.NonSerialized] public bool debugSprint, debugJump, debugCrouch;

    CharacterController cc;
    Transform cam;
    Vector3 velocity;
    float verticalSpeed;
    float stepAccum;
    float lastGroundedTime = -10f;
    float jumpRequestedTime = -10f;
    readonly Collider[] overlapBuf = new Collider[8];

    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsGrounded => cc != null && cc.isGrounded;
    public float CurrentHeight => cc != null ? cc.height : standHeight;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.stepOffset = stepHeight;
        cc.radius = Mathf.Min(cc.radius, 0.25f);   // 導航網格的半徑是 0.3：膠囊要比它小，窄縫才過得去
        cc.skinWidth = 0.04f;
        cc.minMoveDistance = 0f;   // 預設 0.001：幀率很高時每幀移動量低於它會被整個忽略（走不動）
        cc.height = standHeight;
        cc.center = new Vector3(0, standHeight * 0.5f, 0);
        var c = GetComponentInChildren<Camera>();
        if (c != null) cam = c.transform;
    }

    void Update()
    {
        Vector2 input = Vector2.zero;
        bool sprintHeld = false, crouchHeld = false, jumpPressed = false;
        bool gameplay = GameManager.Instance.Mode == InputMode.Gameplay;

        if (debugInput.HasValue)
        {
            input = debugInput.Value; sprintHeld = debugSprint; crouchHeld = debugCrouch; jumpPressed = debugJump;
            debugJump = false;
        }
        else if (gameplay)
        {
            input = GameInput.Move.ReadValue<Vector2>();
            sprintHeld = GameInput.Sprint.IsPressed();
            crouchHeld = GameInput.Crouch.IsPressed();
            jumpPressed = GameInput.Jump.WasPressedThisFrame();
        }
        input = Vector2.ClampMagnitude(input, 1f);

        // ── 蹲下 ──
        if (crouchHeld) IsCrouching = true;
        else if (IsCrouching && CanStandUp()) IsCrouching = false;

        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        if (!Mathf.Approximately(cc.height, targetHeight))
        {
            cc.height = Mathf.MoveTowards(cc.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
        }
        if (cam != null)
        {
            float k = Mathf.InverseLerp(crouchHeight, standHeight, cc.height);
            var lp = cam.localPosition;
            lp.y = Mathf.Lerp(cameraCrouchY, cameraStandY, k);
            cam.localPosition = lp;
        }

        // ── 水平移動 ──
        IsSprinting = sprintHeld && !IsCrouching && input.sqrMagnitude > 0.01f;
        float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
        velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-acceleration * Time.deltaTime));

        // ── 跳躍 / 重力 ──
        if (cc.isGrounded) lastGroundedTime = Time.time;
        if (jumpPressed) jumpRequestedTime = Time.time;

        bool canJump = !IsCrouching && Time.time - lastGroundedTime <= coyoteTime;
        if (canJump && Time.time - jumpRequestedTime <= jumpBuffer)
        {
            verticalSpeed = Mathf.Sqrt(2f * -gravity * jumpHeight);
            jumpRequestedTime = -10f;
            lastGroundedTime = -10f;
            AudioManager.Play(Sfx.Footstep, 0.6f);
        }
        else if (cc.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;

        cc.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);

        // 頭撞到天花板就不要繼續往上
        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;

        // ── 腳步聲 ──
        if (cc.isGrounded && velocity.sqrMagnitude > 0.6f)
        {
            stepAccum += velocity.magnitude * Time.deltaTime;
            if (stepAccum >= stepLength)
            {
                stepAccum = 0f;
                AudioManager.Play(Sfx.Footstep, IsCrouching ? 0.12f : (IsSprinting ? 0.55f : 0.35f));
            }
        }
    }

    /// <summary>頭上有足夠的空間可以站起來嗎？（不算自己、守衛）</summary>
    bool CanStandUp()
    {
        float r = cc.radius;
        var p0 = transform.position + Vector3.up * (r + 0.02f);
        var p1 = transform.position + Vector3.up * (standHeight - r);
        int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r * 0.95f, overlapBuf, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = overlapBuf[i];
            if (c == cc || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<GuardAI>() != null) continue;
            return false;
        }
        return true;
    }

    // 除錯用：最近一次撞到的非地面物件（測試腳本卡住時會印出來）
    public string LastBlockerInfo { get; private set; } = "";
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.7f) return;
        LastBlockerInfo = hit.collider.name + " n=" + hit.normal.ToString("F2") + " p=" + hit.point.ToString("F2") + " flags=" + cc.collisionFlags + " grounded=" + cc.isGrounded;
    }

    public void StopImmediately()
    {
        velocity = Vector3.zero;
        verticalSpeed = 0f;
    }
}
