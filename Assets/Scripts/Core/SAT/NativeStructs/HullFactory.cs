
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace CombatCore.Core
{
    // 详细面定义结构体
    public struct DetailedFaceDef
    {
        public Vector3 Center; // 面中心点（三角形面）
        public Vector3 Normal; // 面法线
        public List<float3> Verts; // 面的顶点列表
        public List<int> Indices; // 顶点索引列表
    }

    // 原生面定义结构体(不安全代码)
    public unsafe struct NativeFaceDef
    {
        public int VertexCount; // 顶点数量
        public int* Vertices; // 顶点指针
        public int HighestIndex; // 最高顶点索引(用于优化)
    }

    // 原生凸包定义结构体(不安全代码)
    public unsafe struct NativeHullDef
    {
        public int FaceCount; // 面数量
        public int VertexCount; // 顶点数量
        public NativeArray<float3> VerticesNative; // 顶点原生数组
        public NativeArray<NativeFaceDef> FacesNative; // 面原生数组
    }

    public class HullFactory
    {
        // 从Mesh(网格)数据创建一个凸包(NativeHull)
        // 从 Mesh 顶点和三角形索引数据，计算每个三角形的法线和中心
        // 去重重复顶点，合并具有相同法线且共享顶点的三角形，形成大面
        // 计算每个合并面边界的顶点序列，标记“孤立顶点”并将其剔除
        // 构造最终的顶点和面数据，传入底层方法生成 NativeHull 凸包数据结构
        public static unsafe NativeHull CreateFromMesh(Mesh mesh)
        {
            // 存储详细面的列表(带有顶点、法线等信息)
            var faces = new List<DetailedFaceDef>();
            // 对网格顶点进行舍入处理，防止因浮点数精度导致重复顶点识别失败
            var verts = mesh.vertices.Select(RoundVertex).ToArray();
            // 去除重复顶点，只保留唯一顶点
            var uniqueVerts = verts.Distinct().ToList();
            // 获取网格的三角形索引数组(三个一组)
            var indices = mesh.triangles;

            // 遍历所有三角形，每3个索引为一个三角形，主要为了收集面信息
            for (int i = 0; i < mesh.triangles.Length; i = i + 3)
            {
                // 三角形顶点索引
                var idx1 = i;
                var idx2 = i + 1;
                var idx3 = i + 2;

                // 根据索引获取三角形顶点的坐标
                Vector3 p1 = verts[indices[idx1]];
                Vector3 p2 = verts[indices[idx2]];
                Vector3 p3 = verts[indices[idx3]];

                // 以 p2 为公共起点构造两条三角形边，叉乘得到该三角形法线，并归一化
                var normal = math.normalize(math.cross(p3 - p2, p1 - p2));
                // 对法线进行舍入，防止法线因为浮点误差微小不同而无法正确归类
                var roundedNormal = RoundVertex(normal);

                // 创建一个详细面定义，包含中心点、法线、顶点列表和顶点索引
                faces.Add(new DetailedFaceDef
                {
                    Center = (p1 + p2 + p3) / 3, // 面中心点(重心公式)
                    Normal = roundedNormal,
                    Verts = new List<float3> { p1, p2, p3 },
                    Indices = new List<int>
                    {
                        uniqueVerts.IndexOf(p1), // 顶点在唯一顶点列表中的索引
                        uniqueVerts.IndexOf(p2),
                        uniqueVerts.IndexOf(p3),
                    }
                });
            }

            // 创建一个存储最终合并面定义的列表
            var faceDefs = new List<NativeFaceDef>();
            // 用来记录"孤立"顶点的索引，这些顶点没有任何边界连接
            var orphanIndices = new HashSet<int>();
            // 先根据法线分组，再根据共享顶点分组，合并具有相同法线和共享顶点的所有面
            var mergedFaces = GroupBySharedVertex(GroupByNormal(faces));

            // 遍历合并后的每组面
            foreach (var faceGroup in mergedFaces)
            {
                // 收集该组所有面中所有顶点的索引，SelectMany 会将所有 face.Indices 扁平化一个单一的集合(flat list),而不是一个集合的集合
                var indicesFromMergedFaces = faceGroup.SelectMany(face => face.Indices).ToArray();
                // 计算顶点形成的多边形的边界轮廓(边界顶点序列)
                var border = PolygonPerimeter.CalculatePerimeter(indicesFromMergedFaces);
                // 获取边界顶点的索引列表，提取EndIndex组成新数组
                // var borderIndices = 

            }
        }

        // 按法线分组
        public static Dictionary<float3, List<DetailedFaceDef>> GroupByNormal(IList<DetailedFaceDef> data)
        {
            var map = new Dictionary<float3, List<DetailedFaceDef>>();
            for (var i = 0; i < data.Count; i++)
            {
                var item = data[i];
                // 第一次遇到这个法线 进入 创建 新的列表容器
                if (!map.TryGetValue(item.Normal, out List<DetailedFaceDef> value))
                {
                    map[item.Normal] = new List<DetailedFaceDef> { item };
                    continue;
                }
                value.Add(item);
            }
            return map;
        }

        // 按共享顶点分组
        // 输入参数 groupedFaces 是一个字典，键是法线方向(float3)，值是所有具有该法线的面(DetailedFaceDef列表)
        // 这里主要把已经按法线归类的面 进一步按法线相同且有“顶点连接”的面归并在一起
        public static List<List<DetailedFaceDef>> GroupBySharedVertex(Dictionary<float3, List<DetailedFaceDef>> groupedFaces)
        {
            // 最终结果：每组是若干个法线相同，且共享顶点的面
            var result = new List<List<DetailedFaceDef>>();

            // 遍历每个法线分组(每个法线方向相同的一组面)
            foreach (var faceSharingNormal in groupedFaces)
            {
                // 临时map, 每个元素包含: 
                // - 一个 HashSet<int> 用于记录当前面组中所有顶点的索引(用于判断是否与其他面共享)
                // - 一个面列表 List<DetailedFaceDef> 存储当前组的所有面
                var map = new List<(HashSet<int> Key, List<DetailedFaceDef> Value)>();

                // 遍历当前法线下的所有面
                foreach (var face in faceSharingNormal.Value)
                {
                    // 尝试查找当前面是否与已有组共享顶点
                    var group = map.FirstOrDefault(pair => face.Indices.Any(pair.Key.Contains));
                    if (group.Key != null)
                    {
                        // 如果找到了共享顶点的组, 将当前面的所有顶点加入该组的顶点集合
                        foreach (var idx in face.Indices)
                        {
                            group.Key.Add(idx);
                        }
                        // 把当前面加入该组中
                        group.Value.Add(face);
                    }
                    else
                    {
                        // 没有共享顶点的组，就创建一个新组，把当前面作为第一项
                        map.Add((new HashSet<int>(face.Indices), new List<DetailedFaceDef> { face }));
                    }
                }

                // 把该法线方向下的所有哦合并组加入结果列表
                // Range 添加一组元素到集合末尾
                result.AddRange(map.Select(group => group.Value));
            }
            return result;
        }

        // 顶点坐标舍入，保留小数点后3位
        public static float3 RoundVertex(Vector3 v)
        {
            return new float3(
                (float)System.Math.Round(v.x, 3),
                (float)System.Math.Round(v.y, 3),
                (float)System.Math.Round(v.z, 3));
        }

        // 多边形周长计算结构
        public struct PolygonPerimeter
        {
            public struct Edge
            {
                public int StartIndex;
                public int EndIndex;
            }
            
            private static readonly List<Edge> OutsideEdges = new List<Edge>();

            // 计算多边形的边界周长(即外部边缘的有序列表)
            // 参数:
            //  indices: 由三角面组成的索引数组(每 3 个为一组三角形)
            // 返回:
            //  外部边缘(即构成多边形轮廓的边)列表，已按顺序排列形成闭环
            public static List<Edge> CalculatePerimeter(int[] indices)
            {
                // 清空全局临时边列表 OutsideEdges (存储的是最终"边界边", 不是三角形内部边)
                OutsideEdges.Clear();

                // 遍历所有三角形(每3个索引构成一个三角形)
                for(int i = 0; i < indices.Length - 1; i += 3)
                {
                    int v1 = indices[i];
                    int v2 = indices[i + 1];
                    int v3 = indices[i + 2];

                    // 将三角形的三条边尝试加入外部边集合
                    AddOutsideEdge(v1, v2); // 边 v1 -> v2
                    AddOutsideEdge(v2, v3); // 边 v2 -> v3
                    AddOutsideEdge(v3, v1); // 边 v3 -> v1
                }

                // 检查这些边是否构成一个连续的闭合边界(顺时针和逆时针)
                for (int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = OutsideEdges[i];
                    var nextIdx = i + 1 > OutsideEdges.Count - 1 ? 0 : i + 1; // 最后一个边指向第一个 闭环
                    var next = OutsideEdges[nextIdx];

                    // 如果当前边的终点不是下一个边的起点, 说明边界不连续, 需重构
                    if(edge.EndIndex != next.StartIndex)
                    {
                        return Rebuild(); // 尝试按连通方式重新排序边
                    }
                }

                // 所有边已经按顺序闭合, 直接返回
                return OutsideEdges; 
            }

            // 添加一个边到外部集合中(用于构建多边形的边界)
            // 如果该边的反向边已经存在, 说明这是一个内部边, 将其从集合中移除
            // 否则将其作为边界边添加进去
            private static void AddOutsideEdge(int i1, int i2)
            {
                // 遍历当前已记录的外边集合
                foreach(var edge in OutsideEdges)
                {
                    // 如果当前边是反向边(i2 -> i1)或正向重复(i1 -> i2), 说明这条边已经成对出现
                    if((edge.StartIndex == i1 && edge.EndIndex == i2) || (edge.StartIndex == i2 && edge.EndIndex == i1))
                    {
                        // 已存在这条边或其反向边, 说明它是两个三角形共享的"内部边"     
                        // 将它从外部边集合中移除(最终只保留非共享的"轮廓边")
                        OutsideEdges.Remove(edge);
                        return; // 退出，已经处理完这条边             
                    }
                }

                // 如果上面没有找到匹配的边, 则添加为"外部边"
                OutsideEdges.Add(new Edge{ StartIndex = i1, EndIndex = i2 });
            }
            
            // 重建边的顺序, 使它们按连续顺序连接成一个闭合的轮廓边链
            private static List<Edge> Rebuild()
            {
                // 新的边集合(用于存放重建后的有序边)
                var result = new List<Edge>();

                // 构建一个从起点索引到终点索引的映射字典
                // 用于快速查找给定起点对应的终点(边的连接关系)
                var map = OutsideEdges.ToDictionary(k => k.StartIndex, v => v.EndIndex);

                // 从第一条边起点开始构造链
                // First LINQ获取第一个元素 那为什么不用OutsideEdges[0]呢？
                // var cur = OutsideEdges.First().StartIndex;
                var cur = OutsideEdges[0].StartIndex;

                // 依次构造每一条边, 使它们首尾相接
                for(int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = new Edge
                    {
                        StartIndex = cur, // 当前边的起点
                        EndIndex = map[cur] // 当前边的终点，在映射中查询                        
                    };

                    // 添加到结果集合中
                    result.Add(edge);

                    // 将当前终点设置为下一条边起点，继续连边
                    cur = edge.EndIndex;
                }

                // 返回重建后 有序的边集合(如将两个共面三角形，整合成一个外围多边形轮廓)
                return result;
            }
        }
    }
}