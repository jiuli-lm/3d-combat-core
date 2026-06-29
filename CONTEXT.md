# CombatCore — 项目上下文

学习 Unity 3D 战斗系统开发的练习项目。

## 项目简介

- **项目名**：CombatCore
- **引擎**：Unity 6（URP 渲染管线）
- **类型**：3D 动作战斗核心框架
- **用途**：个人学习，非商业项目

## 代码目录

```
Assets/
├── Scripts/
│   ├── Characters/   # 角色相关脚本（玩家、敌人）
│   ├── Core/         # 核心系统（状态机、战斗逻辑）
│   ├── Managers/     # 管理器（GameManager 等）
│   └── SATHull/      # SAT 碰撞检测
├── Scenes/           # 场景文件
├── Models/           # 3D 模型
├── Animations/       # 动画资源
├── Materials/        # 材质
└── InputActions/     # 输入配置
```

## 术语表

| 术语 | 说明 |
|------|------|
| FSM | 有限状态机（Finite State Machine） |
| SAT | 分离轴定理（Separating Axis Theorem），用于碰撞检测 |
| 凸包 | 包围一组点或几何体的最小凸形状，当前用于 SAT 碰撞检测的数据表达 |
| 凸包数据 | 用于描述凸包几何与拓扑关系的数据集合，作为 SAT 碰撞检测的输入 |
| 凸包面 | 凸包边界上的一个平面多边形区域，用于表达凸包的一部分外表面 |
| 半边 | 凸包拓扑中的有向边，用于连接顶点、凸包面以及相邻面的关系 |
| URP | Universal Render Pipeline |
| Behaviour Tree | 行为树，AI 决策用 |

## 参考资源

- 视频教程：[待补充]
- 项目规划：`agent/项目规划.md`
