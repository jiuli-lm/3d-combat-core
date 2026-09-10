using System;
using Unity.Mathematics;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CombatCore.Core
{
    // 定义调试标志的枚举类型, 用于控制在调试过程中显示的内容
    [Flags]
    public enum DebugHullFlags
    {
        None = 0, // 无标志
        PlaneNormals = 2, // 显示平面法线
        Indices = 4, // 显示索引
        Outline = 8, // 显示轮廓
        All = ~0, // 所有标志
    }

    public class HullDrawingUtility
    {
        // 根据选项绘制调试 Hull(凸包), 外部轮廓方便计算
        public static void DarwDebugHull(NativeHull hull, RigidTransform t, DebugHullFlags options = DebugHullFlags.All,
            Color BaseColor = default)
        {
            if(!hull.IsValid)
                throw new ArgumentException("Hull is not valid", nameof(hull));
            
            if(options == DebugHullFlags.None) return;

            if(BaseColor == default)
                BaseColor = Color.yellow;
            
            // 遍历每对边, 所以迭代为+2
            for (int j = 0; j < hull.EdgeCount; j = j + 2)
            {
                var edge = hull.GetEdge(j);
                var twin = hull.GetEdge(j + 1);

                // hull.GetVertex 获取局部空间位置, 进而用math.transform根据t来转换为世界空间
                var edgeVertex1 = math.transform(t, hull.GetVertex(edge.Origin));
                var twinVertex1 = math.transform(t, hull.GetVertex(twin.Origin));

                if ((options & DebugHullFlags.Outline) != 0)
                {
                    Debug.DrawLine(edgeVertex1, twinVertex1, BaseColor);
                }

            }
        }
    }
}
