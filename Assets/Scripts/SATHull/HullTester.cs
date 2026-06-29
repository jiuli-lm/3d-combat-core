using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using CombatCore.Core;



#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode] // 在编辑模式下执行
public class HullTester : MonoBehaviour
{
    public List<Transform> Transforms; // 要测试的节点列表

    [Header("可视化选项")]
    public bool DrawIsCollided; // 绘制碰撞状态
    public bool DrawIntersection; // 绘制相交区域

    [Header("控制台日志")]
    public bool LogContect; // 记录接触日志

    private Dictionary<int, TestShape> Hulls; // 凸包字典(Key: 实例ID, Value: TestShape测试形状)

    // 手动复用 哈希集合去重 一般不会手动赋值可以用只读
    private readonly List<Transform> transforms = new();
    private readonly HashSet<Transform> transformSet = new();

    void Update()
    {
        HandleTransformChanged(); // 处理节点变化
        HandleHullCollisions(); // 处理凸包碰撞
    }

    private void HandleTransformChanged()
    {
        // // Transforms.ToList() 为了得到副本不影响原来的, Distinct去重一下, 然后只保留激活状态的节点 输出一个List表
        // // 这里没做缓存来减少GC
        // var transforms = Transforms.ToList().Distinct().Where(t => t.gameObject.activeSelf).ToList();

        // 尝试改为 手动复用缓存
        transforms.Clear();
        transformSet.Clear();

        for(int i = 0; i < Transforms.Count; i++)
        {
            var t = Transforms[i];

            if(t == null)
                continue;
            if(!t.gameObject.activeSelf)
                continue;
            if(!transformSet.Add(t))
                continue;
            
            transforms.Add(t);
        }

        var newTransformFound = false;
        var transformCount = 0;

        // 过滤下有没有新的 看看要不要重建
        if(Hulls != null)
        {
            for(var i = 0; i < transforms.Count; i++)
            {
                var t = transforms[i];
                if(t == null)
                    continue;

                transformCount++;

                var foundNewHull = !Hulls.ContainsKey(t.GetInstanceID());
                if (foundNewHull)
                {
                    newTransformFound = true;
                    break;
                }
            }
            if(!newTransformFound && transformCount == Hulls.Count)
                return;
        }

        // 经历过上面的判断还能到这里就是需要重建了
        Debug.Log("重建对象");

        // 安全的释放资源
        EnsureDestroyed();

        // 保存下为空的节点，InstanceID作为key，创建的TestShape作为Value记录在字典里，方便后续使用
        // Hulls = transforms.Where(t=>t!=null).ToDictionary(k=>k.GetInstanceID(), CreateShape);
        Hulls ??= new Dictionary<int, TestShape>();
        Hulls.Clear();

        for (int i = 0; i < transforms.Count; i++)
        {
            var t = transforms[i];

            if (t == null)
                continue;

            Hulls.Add(t.GetInstanceID(), CreateShape(t));
        }

        // 强制重新绘制场景视图
        SceneView.RepaintAll();
    }
    
    // 创建测试形状
    private TestShape CreateShape(Transform t)
    {
        var hull = CreateHull(t);

        return new TestShape
        {
            Id = t.GetInstanceID(),
            Hull = hull,
        };
    }

    // 根据变换创建凸包
    private NativeHull CreateHull(Transform v)
    {
        // 怎么知道网格信息呢
        // 获取Collider 还要看是meshCollider还是meshFilter
        var collider = v.GetComponent<Collider>();
        if(collider is MeshCollider meshCollider)
        {
            // return 一个NativeHull 然后create 用meshColloder的sharedMesh
        }

        var mf = v.GetComponent<MeshFilter>();  
        if(mf != null && mf.sharedMesh != null)
        {
            // return 一个NativeHull 然后create 用meshCollider的
        }
        throw new InvalidOperationException($"无法从游戏对象'{v?.name}'创建凸包");
    }

    // 处理凸包碰撞检测
    private void HandleHullCollisions()
    {
        
    }

    // 确保资源被销毁
    private void EnsureDestroyed()
    {
        
    }
}
