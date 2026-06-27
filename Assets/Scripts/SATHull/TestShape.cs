
using System;
using CombatCore.Core;
using Unity.Burst;

[BurstCompile] // 启用 Burst 编译器优化此结构体性能
// IEquatable 是为了重载 Equals 判等操作 而 IComparable 是为了CompareTo比较
public struct TestShape : IEquatable<TestShape>, IComparable<TestShape>
{
    // 唯一标识符
    public int Id;
    // 形状的外壳
    public NativeHull hull;
    
    // 比较两个 TestShape 是否相等
    public int CompareTo(TestShape other)
    {
        return Id.CompareTo(other.Id);
    }

    public bool Equals(TestShape other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is TestShape shape && shape.Equals(this);
    }

    public override int GetHashCode()
    {
        return Id;
    }
}