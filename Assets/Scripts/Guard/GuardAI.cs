using UnityEngine;
using UnityEngine.AI;

public enum GuardState { Patrol, Alert, Chase, Search }

/// <summary>
/// 守衛 AI：Patrol 巡邏 → Alert 警戒 → Chase 追逐 → Search 搜尋 → 回到 Patrol。
/// 第二關 1 名（巡邏），第四關 3 名（startInChase + alwaysKnowsPlayer）共用同一支腳本。
/// NavMeshAgent 在 Prefab 內預設是關閉的，等 RuntimeNavMesh 烘焙完成後才啟用。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class GuardAI : MonoBehaviour
{
    [Header("巡邏")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float waitAtPoint = 1.2f;

    [Header("追逐")]
    public float chaseSpeed = 4.6f;
    [Tooltip("true = 一直知道玩家位置（第四關）")]
    public bool alwaysKnowsPlayer;
    [Tooltip("true = 一開始就追逐（第四關）")]
    public bool startInChase;
    bool waitingForRoom;
    public bool CanCatch => !waitingForRoom;
    public float chaseStartDelay = 3f;

    [Header("感知")]
    public Transform eye;
    public float sightRange = 14f;
    public float fieldOfView = 110f;
    public float closeSenseRange = 2.5f;
    public float loseSightTime = 3f;
    public float alertTime = 0.7f;
    public float searchTime = 5f;

    public GuardState State { get; private set; } = GuardState.Patrol;

    NavMeshAgent agent;
    Vector3 startPosition;
    Quaternion startRotation;
    int patrolIndex;
    float waitTimer, timer, loseTimer, repathTimer, delayTimer;
    bool initialized, delaying;
    Vector3 lastKnown;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        startPosition = transform.position;
        startRotation = transform.rotation;
        if (eye == null) eye = transform;
    }

    void OnEnable()
    {
        initialized = false;
    }

    void Update()
    {
        if (!initialized) { TryInit(); return; }
        if (GameManager.Instance.State != GameState.Playing) return;

        var player = PlayerCheckpoint.Instance;
        if (player == null) return;

        Vector3 pPos = player.transform.position;
        if (waitingForRoom)
        {
            bool entered = pPos.x >= 32f && pPos.x <= 50f && pPos.z >= 30f && pPos.z <= 51f;
            if (!entered) return;
            waitingForRoom = false;
            BeginChase(0f);
        }
        bool sees = CanSee(pPos);
        if (sees) lastKnown = pPos;

        if (delaying)
        {
            delayTimer -= Time.deltaTime;
            FaceTowards(pPos);
            if (delayTimer <= 0f) { delaying = false; EnterChase(); }
            return;
        }

        switch (State)
        {
            case GuardState.Patrol: UpdatePatrol(sees); break;
            case GuardState.Alert: UpdateAlert(); break;
            case GuardState.Chase: UpdateChase(sees, pPos); break;
            case GuardState.Search: UpdateSearch(sees); break;
        }
    }

    // ───────────── 初始化 / 重置 ─────────────

    void TryInit()
    {
        if (!RuntimeNavMesh.Ready) return;

        if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas))
            transform.position = hit.position;

        agent.enabled = true;
        agent.autoBraking = false;
        agent.stoppingDistance = 0.2f;
        agent.angularSpeed = 360f;
        agent.acceleration = 14f;
        initialized = true;

        ResetBehaviour();
    }

    public void ResetToStart()
    {
        if (!initialized) return;
        agent.Warp(startPosition);
        transform.rotation = startRotation;
        patrolIndex = 0;
        ResetBehaviour();
    }

    void ResetBehaviour()
    {
        waitingForRoom = startInChase && alwaysKnowsPlayer;
        if (waitingForRoom)
        {
            agent.ResetPath();
            State = GuardState.Alert;
        }
        else if (startInChase) BeginChase(chaseStartDelay);
        else EnterPatrol();
    }

    public void BeginChase(float delay)
    {
        agent.ResetPath();
        loseTimer = 0f;
        if (delay > 0f)
        {
            State = GuardState.Alert;
            delaying = true;
            delayTimer = delay;
        }
        else
        {
            delaying = false;
            EnterChase();
        }
    }

    // ───────────── 狀態 ─────────────

    void EnterPatrol()
    {
        State = GuardState.Patrol;
        delaying = false;
        waitTimer = 0f;
        agent.speed = patrolSpeed;
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[patrolIndex % patrolPoints.Length].position);
    }

    void UpdatePatrol(bool sees)
    {
        agent.speed = patrolSpeed;
        if (sees) { EnterAlert(); return; }
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtPoint)
            {
                waitTimer = 0f;
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }
    }

    void EnterAlert()
    {
        State = GuardState.Alert;
        timer = alertTime;
        agent.ResetPath();
        AudioManager.Play(Sfx.Alarm, 0.5f);
    }

    void UpdateAlert()
    {
        FaceTowards(lastKnown);
        timer -= Time.deltaTime;
        if (timer <= 0f) EnterChase();
    }

    void EnterChase()
    {
        State = GuardState.Chase;
        loseTimer = 0f;
        repathTimer = 0f;
        agent.speed = chaseSpeed;
    }

    void UpdateChase(bool sees, Vector3 pPos)
    {
        agent.speed = chaseSpeed;

        if (sees || alwaysKnowsPlayer)
        {
            loseTimer = 0f;
            lastKnown = pPos;
        }
        else
        {
            loseTimer += Time.deltaTime;
            if (loseTimer > loseSightTime) { EnterSearch(); return; }
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = 0.15f;
            agent.SetDestination(lastKnown);
        }
    }

    void EnterSearch()
    {
        State = GuardState.Search;
        timer = searchTime;
        agent.speed = patrolSpeed * 1.5f;
        agent.SetDestination(lastKnown);
    }

    void UpdateSearch(bool sees)
    {
        if (sees) { EnterAlert(); return; }
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
        {
            transform.Rotate(0f, 90f * Time.deltaTime, 0f); // 原地張望
            timer -= Time.deltaTime;
            if (timer <= 0f) EnterPatrol();
        }
    }

    // ───────────── 感知 ─────────────

    bool CanSee(Vector3 playerPos)
    {
        // 蹲著：目標點降低（會被矮掩體擋住），視距與近距離感知也縮短
        var pm = PlayerCheckpoint.Instance != null ? PlayerCheckpoint.Instance.GetComponent<PlayerMovement>() : null;
        bool crouching = pm != null && pm.IsCrouching;
        float range = crouching ? sightRange * 0.7f : sightRange;
        float close = crouching ? closeSenseRange * 0.6f : closeSenseRange;

        Vector3 target = playerPos + Vector3.up * (crouching ? 0.7f : 1.2f);
        Vector3 from = eye.position;
        Vector3 to = target - from;
        float dist = to.magnitude;
        if (dist > range) return false;

        Vector3 flat = new Vector3(to.x, 0f, to.z);
        bool inCone = flat.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, flat) <= fieldOfView * 0.5f;
        if (!inCone && dist > close) return false;

        if (Physics.Linecast(from, target, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform.GetComponentInParent<PlayerCheckpoint>() != null;
        return true;
    }

    void FaceTowards(Vector3 worldPos)
    {
        Vector3 d = worldPos - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 8f * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, closeSenseRange);
    }
}
