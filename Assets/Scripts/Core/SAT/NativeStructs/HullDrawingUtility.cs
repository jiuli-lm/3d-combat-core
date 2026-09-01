using System; 

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
        
    }
}