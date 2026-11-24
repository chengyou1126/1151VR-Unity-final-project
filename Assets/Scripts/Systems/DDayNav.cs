using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 導航網格設定（執行時與編輯器共用，確保「編輯器驗證過的位置」在遊戲中也走得到）。
/// 以玩家尺寸烘焙：半徑 0.3、高度 1.8、可跨 0.3 的階梯、坡度 50°（比這高的要用跳的）。
/// </summary>
public static class DDayNav
{
    public static NavMeshBuildSettings Settings()
    {
        var s = NavMesh.GetSettingsByID(0);
        s.agentRadius = 0.3f;
        s.agentHeight = 1.8f;
        s.agentClimb = 0.3f;   // 要和玩家的 stepOffset 一致（0.5 會讓角色控制器的頭頂到 2 公尺高的門楣而卡住）
        s.agentSlope = 50f;
        return s;
    }

    public static NavMeshData Build(Transform root)
    {
        // 觸發區（ProximityZone 的範圍盒）不是實體，不能算進導航網格，否則整個房間會被當成障礙物
        var markups = new List<NavMeshBuildMarkup>();
        foreach (var z in root.GetComponentsInChildren<ProximityZone>(true))
            markups.Add(new NavMeshBuildMarkup { root = z.transform, ignoreFromBuild = true });

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);

        Bounds b = new Bounds(root.position, Vector3.one);
        bool first = true;
        foreach (var c in root.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (first) { b = c.bounds; first = false; } else b.Encapsulate(c.bounds);
        }
        b.Expand(4f);
        return NavMeshBuilder.BuildNavMeshData(Settings(), sources, b, Vector3.zero, Quaternion.identity);
    }
}
