using Unity.Mathematics;
namespace CombatCore.Core
{
    // 面查询结果结构体
    public struct FaceQueryResult
    {
        public int Index; // 面索引
        public float Distance; // 面距离
    }

    // 边查询结果结构体
    public struct EdgeQueryResult
    {
        public int Index1; // 边1的起始顶点索引
        public int Index2; // 边1的终止顶点索引
        public float Distance; // 边之间的距离
    }


    // 碰撞信息结构体
    public struct CollisionInfo
    {
        public bool IsColliding; // 是否发生碰撞
        public FaceQueryResult Face1; // 第一个查询结果
        public FaceQueryResult Face2; // 第二个查询结果
        public EdgeQueryResult Edge; // 边查询结果 
             
    }

    public class HullCollision
    {
        // 获取调试用的碰撞信息
        public static CollisionInfo GetDebugCollisionInfo(RigidTransform transform1, NativeHull hull1, 
            RigidTransform transform2, NativeHull hull2)
        {
            CollisionInfo result = default;
            QueryFaceDistance(out result.Face1, transform1, hull1, transform2, hull2);
            QueryFaceDistance(out result.Face2, transform2, hull2, transform1, hull1);
            result.IsColliding = result.Face1.Distance < 0 &&  result.Face2.Distance < 0;
            return result;
        }

        // 查询两个体之间的面距离, 主要就是再找分离轴, 通过每个面法线(即 plane.Normal )作为潜在的分离轴来进行计算的
        public static unsafe void QueryFaceDistance(out FaceQueryResult result, RigidTransform transform1, 
            NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // 在第二个体的局部空间中进行计算
            RigidTransform transform = math.mul(math.inverse(transform2), transform1);

            result.Distance = -float.MaxValue; // 初始化至最远距离
            result.Index = -1; // 初始化索引

            // 实际上这个for就是再选一组分离轴, 按个算. 遍历hull1的每个面, 每个面的法线 plane.Normal 被当作一个分离轴
            for(int i = 0; i < hull1.FaceCount; ++i)
            {
                // 获取面平面
                NativePlane plane = transform * hull1.GetPlane(i);
                // 获取支撑点, 注意这里是用的hull2调用的hull1面的法线,
                // 也就是说目的是找到 hull2 在这个法线反方向上最远的点(即最靠近 hull1 面的点).
                // 可以进到这个GetSupport函数去看, 不断地dot这个内法线, 值最大的就是距离最近的, 返回值是点的坐标
                // 本质上是点到直线的距离
                float3 support = hull2.GetSupport(-plane.Normal);
                // 计算面到支撑点的距离，法线目前就当做是探索的分离轴
                // support 点沿着分离轴的投影值 - hull 当前面的投影值, 也就是这两个投影区间的距离
                float distance = plane.Distance(support);

                // 当 distance > 0时, 表示: 
                // support 落在了 hull1 这个面所在的平面的"外面"; 也就是在这个轴上存在投影间隙;
                // 也就是 SAT 中的"分离轴存在"

                // 当 distance <= 0时, 表示: 
                // support 落在了 hull1 面平面内或上面;
                // 在这个轴上投影区间重叠: 需要继续检测其它轴

                // 更新最大距离和面索引, 所以保存好最大的就行了, 知道有存在的即可, 全部小于0就是真的分不开
                if(distance > result.Distance)
                {
                    result.Distance = distance;
                    result.Index = i;
                }
            }
        }
    }
}  