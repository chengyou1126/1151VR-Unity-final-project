using UnityEngine;

/// <summary>
/// 匯入的人形模型是 T-Pose，這裡在執行時把兩隻手臂放下來（沒有動畫檔時的替代做法）。
/// 之後有 Idle 動畫就可以把這個元件移掉。
/// </summary>
public class RelaxedPose : MonoBehaviour
{
    public string leftUpper = "LeftArm", leftFore = "LeftForeArm";
    public string rightUpper = "RightArm", rightFore = "RightForeArm";
    [Range(0f, 1f)] public float swingForward = 0.08f;

    void Start()
    {
        Apply(leftUpper, leftFore);
        Apply(rightUpper, rightFore);
    }

    void Apply(string upperName, string foreName)
    {
        Transform upper = Find(transform, upperName), fore = Find(transform, foreName);
        if (upper == null || fore == null) return;
        Vector3 current = (fore.position - upper.position).normalized;
        Vector3 target = (Vector3.down + transform.forward * swingForward).normalized;
        upper.rotation = Quaternion.FromToRotation(current, target) * upper.rotation;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
