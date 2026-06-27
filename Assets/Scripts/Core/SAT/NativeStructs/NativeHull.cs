

using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace CombatCore.Core
{
    // 表示一个原生的几何体
    public unsafe struct NativeHull
    {
        public int VertexCount; // 顶点数量
        public int FaceCount; // 面数量
        public int EdgeCount; // 边数量

        // Unity 为 NativeArray, NativeList 等 Native 容器 提供了一种叫做 泄露检测(Leak Detection) 的机制
        // 用于检查 Native 内存是否在使用结束后正确 Dispose
        // 以下是 用于告诉 NativeArray 在构造时跳过 LeakDetection 注册，用来提高速度
        public NativeArrayNoLeakDetection<float3> VerticesNative; // 顶点数组
        public NativeArrayNoLeakDetection<NativeFace> FacesNative; // 面数组
        public NativeArrayNoLeakDetection<NativePlane> PlanesNative; // 平面数组
        public NativeArrayNoLeakDetection<NativeHalfEdge> EdgesNative; // 半边数组

        // 以下是直接指向内存的指针，供高效访问使用
        [NativeDisableUnsafePtrRestriction]
        public float3* Vertices; // 顶点指针

        [NativeDisableUnsafePtrRestriction]
        public NativeFace* Faces; // 面指针

        [NativeDisableUnsafePtrRestriction]
        public NativePlane* Planes; // 平面指针

        [NativeDisableUnsafePtrRestriction]
        public NativeHalfEdge* Edges; // 半边指针

        private int _isCreated; // 标记该结构体是否已创建
        private int _isDisposed; // 标记该结构体是否已释放

        // 判断结构体是否已创建
        public bool IsCreated
        {
            get => _isCreated == 1;
            set => _isCreated = value ? 1 : 0;
        }

        // 判断结构体是否已释放
        public bool IsDisposed
        {
            get => _isDisposed == 1;
            set => _isDisposed = value ? 1 : 0;
        }

        // 判断该结构体是否有效（即已创建且未释放）
        public bool IsValid => IsCreated && !IsDisposed;

        // 释放内存
        public void Dispose()
        {
            if(_isDisposed == 0)
            {
                _isDisposed = 1;

                if(VerticesNative.IsCreated)
                    VerticesNative.Dispose();

                if(FacesNative.IsCreated)
                    FacesNative.Dispose();

                if(PlanesNative.IsCreated)
                    PlanesNative.Dispose();

                if(EdgesNative.IsCreated)
                   EdgesNative.Dispose();

                Vertices = null;
                Faces = null;
                Planes = null;
                Edges = null;               
            }
        }
    }
}