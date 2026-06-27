using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;


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
    
    // 缓存节点列表 避免在Update里面的持续GC
    private List<Transform> transforms = new();

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
        transforms.Clear();
        transforms = Transforms.ToList().Distinct().Where(t => t.gameObject.activeSelf).ToList();
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
    }
    
    private void HandleHullCollisions()
    {
        
    }

    // 确保资源被销毁
    private void EnsureDestroyed()
    {
        
    }
}
