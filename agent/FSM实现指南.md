# CombatCore 状态机 (FSM) 实现指南

## 概述

一套三层通用的 FSM 框架，放在 `Assets/Scripts/Core/StateMachine/` 下。

**三层分工**:
- **第 1 层 — 游戏全局**: Menu → Loading → Playing → Pause → GameOver
- **第 2 层 — 角色宏观**: Normal / Stunned / Dead
- **第 3 层 — 角色行为**: Idle / Move / Attack / Dodge / Skill / Hurt / Jump（最复杂）

**核心原则**:
- 运行时层纯 C#，不依赖 Unity
- 配置层用 ScriptableObject
- 桥接层用 MonoBehaviour

---

## 一、文件结构

```
Assets/Scripts/Core/StateMachine/
├── Runtime/
│   ├── StateMachineEnums.cs       ← 4 个枚举
│   ├── ParameterStore.cs          ← 参数存储（数据心脏）
│   ├── Condition.cs               ← 条件系统
│   ├── Transition.cs              ← 过渡逻辑（打断核心）
│   ├── State.cs                   ← 运行时状态
│   └── StateMachine.cs            ← 主引擎
├── Config/
│   ├── StateMachineAsset.cs       ← 顶层 SO，[CreateAssetMenu]
│   ├── StateDef.cs                ← 状态定义（序列化）
│   ├── TransitionDef.cs           ← 过渡定义（序列化）
│   ├── ConditionDef.cs            ← 条件定义（序列化）
│   └── ParameterDef.cs            ← 参数定义（序列化 struct）
└── Unity/
    ├── StateMachineController.cs  ← MonoBehaviour 桥接
    └── InputBuffer.cs             ← 输入缓冲
```

命名空间统一: `CombatCore.Core.StateMachine`

---

## 二、分步要点

---

### Step 1: `StateMachineEnums.cs`

```csharp
ParameterType    → Bool, Int, Float, Trigger
CompareOp        → Equals, NotEquals, Less, LessOrEqual, Greater, GreaterOrEqual
ConditionType    → Bool, Int, Float, Trigger, And, Or, Not, Always, Never, Custom
LogicMode        → And, Or
```

**关键点**: `Trigger` 不是 Bool 别名，它有特殊的「读后消耗」行为。

---

### Step 2: `ParameterStore.cs`

FSM 的**数据心脏**。外部输入（按键、速度、血量变化）→ Set 方法写入；条件评估 → Get 方法读出。

#### 2.1 参数以 nameHash 做索引，不是字符串

```
初始化: "Speed".GetHashCode() → hash → Dictionary<int hash, int arrayIndex>
运行时: GetFloat(hash) → 直接从数组按下标读，零 GC
```

所以 `Initialize` 时遍历参数定义两次：
1. 第一次统计各类型数量 → 分配数组
2. 第二次填 hash→id 映射 + 写默认值

每种类型有独立数组:
```csharp
bool[]   _bools;
int[]    _ints;
float[]  _floats;
bool[]   _triggers;  // 底层是 bool，但语义不同
```

#### 2.2 Trigger 的「读后消耗」机制（最关键）

```csharp
public bool GetTrigger(int id)
{
    if (_triggers[id])
    {
        _triggers[id] = false;  // 读到 true → 立即设回 false
        return true;
    }
    return false;
}

public bool PeekTrigger(int id) => _triggers[id];  // 只看不消耗
```

**为什么要这样做?**

玩家按一次攻击键 → `SetTrigger("Attack")`。如果 GetTrigger 不消耗：
- 第 1 帧条件读到 true → 触发过度
- 第 2 帧还没新输入，但 Trigger 仍是 true → 再次触发过度
- 角色在第一帧抽搐

有了读后消耗：第 1 帧读到 true → flag 清零 → 第 2 帧读到 false，不会重复触发。

#### 2.3 ResetConsumedTriggers() — 每帧开头调用

```csharp
public void ResetConsumedTriggers()
{
    for (int i = 0; i < _triggers.Length; i++)
        _triggers[i] = false;
}
```

**解决什么问题?**

场景: 同一帧内玩家先按了攻击、又按了闪避。FSM 先命中了闪避的全局过渡（优先级更高），Attack Trigger 没被任何条件读到，残留到下一帧。两帧后闪避结束回到 Idle，残留的 Attack Trigger 被读到 → 玩家感觉「我没按啊」。

`ResetConsumedTriggers` 保证: 当帧设置的 Trigger，当帧没被任何过渡条件读到 = 丢弃。

#### 2.4 提供两套 API

- 按 id 读写（性能路径，Update 中多用）
- 按 string 读写（便捷路径，内部还是转 hash → id）

```csharp
public void SetFloat(string name, float value)
{
    int id = GetId(name.GetHashCode());
    if (id >= 0) SetFloat(id, value);
}
```

---

### Step 3: `Condition.cs`

条件是 FSM 过渡的「开关」。一个 Transition 挂一组条件，全部满足才触发。

#### 3.1 类继承结构

```
FsmCondition (abstract)
├── ConditionBool        # 参数 == true/false
├── ConditionInt         # 参数 >=/<=/== value
├── ConditionFloat       # 参数 >=/<=/== value
├── ConditionTrigger     # GetTrigger(参数) 读到 true
├── ConditionAnd         # 子条件全部满足 (短路)
├── ConditionOr          # 子条件任一满足 (短路)
├── ConditionNot         # 取反
├── ConditionAlways      # 永远 true (纯 exit-time 过渡用)
└── ConditionNever       # 永远 false (禁用过渡)
```

#### 3.2 基类极简

```csharp
public abstract class FsmCondition
{
    public abstract bool Evaluate(ParameterStore parameters);
}
```

每个子类在构造时把参数 ID 和比较值存好（只做一次，不在 Evaluate 里做复杂计算）。

#### 3.3 ConditionFloat 示例

```csharp
public class ConditionFloat : FsmCondition
{
    int _paramId; CompareOp _op; float _value;

    public override bool Evaluate(ParameterStore p)
    {
        float cur = p.GetFloat(_paramId);
        return _op switch
        {
            CompareOp.Equals         => Mathf.Approximately(cur, _value),
            CompareOp.NotEquals      => !Mathf.Approximately(cur, _value),
            CompareOp.Less           => cur < _value,
            CompareOp.LessOrEqual    => cur <= _value,
            CompareOp.Greater        => cur > _value,
            CompareOp.GreaterOrEqual => cur >= _value,
            _ => false
        };
    }
}
```

**注意**: float 判等必须用 `Mathf.Approximately`，int 不需要。

#### 3.4 ConditionAnd — 短路求值

```csharp
public class ConditionAnd : FsmCondition
{
    FsmCondition[] _conditions;

    public override bool Evaluate(ParameterStore p)
    {
        for (int i = 0; i < _conditions.Length; i++)
            if (!_conditions[i].Evaluate(p))
                return false;  // 短路！后面不评优
        return true;
    }
}
```

**短路求值的重要性**: 后面的条件可能包含 `ConditionTrigger`（有副作用，会消耗 Trigger）。如果前面的条件已经 false，不能让后面的 Trigger 白白被消耗。

ConditionOr 同理，第一个 true 就返回。

#### 3.5 ConditionTrigger 用 GetTrigger（消耗版）

```csharp
public class ConditionTrigger : FsmCondition
{
    int _paramId;

    public override bool Evaluate(ParameterStore p)
        => p.GetTrigger(_paramId);  // 消耗版本
}
```

举例: 过渡 `Idle → Attack` 有两个条件:
```
[AND]
  ├── ConditionFloat("Speed", <, 0.1)
  └── ConditionTrigger("Attack")
```
- Speed = 5 (false) → 短路，Trigger 不被消耗
- Speed = 0 (true) → 评估 Trigger，读到 true → 消耗

---

### Step 4: `Transition.cs`

**战斗手感的关键**。这个类的 `Evaluate` 方法实现了「谁能打断谁」的全部逻辑。

#### 4.1 数据结构

```csharp
public class Transition
{
    public int TargetStateId;
    public int Priority;           // 过渡优先级（通常 = 目标状态的优先级）
    public bool CanInterrupt;      // 同优先级可否打断
    public bool ForceInterrupt;    // 强制打断（绕过一切检查）
    public bool HasExitTime;       // 是否有最小退出时间
    public float ExitTime;         // 归一化退出时间 (0-1)
    public FsmCondition[] Conditions;
}
```

#### 4.2 Evaluate 流程

```
Evaluate(currentState, parameters, normalizedTime):

  ┌─ ForceInterrupt? ─────────────────→ 跳转到条件检查
  │
  ├─ HasExitTime && normalizedTime < ExitTime? → return false
  │
  ├─ 优先级检查:
  │   Priority > currentState.Priority?
  │      → currentState 在打断窗口内? 不在 → return false
  │
  │   Priority == currentState.Priority?
  │      → CanInterrupt == false? → return false
  │      → 不在打断窗口? → return false
  │
  │   Priority < currentState.Priority? → return false
  │
  └─ 条件检查: 遍历所有条件，全部为 true → return true
```

#### 4.3 结合游戏的例子

| 状态 | 优先级 | 打断窗口 |
|------|--------|---------|
| Idle | 0 | 始终可打断 (Duration=0) |
| Move | 1 | 始终可打断 |
| LightAttack | 2 | (0.7, 1.0) — 后 30% 可打断 |
| Hurt | 5 | 不可打断 |
| Dodge | 7 | 不可打断 |

**玩家在普攻 (优先级 2) 中按闪避 (优先级 7)**:
1. 全局过渡 `Dodge (7)` 被检查
2. 7 > 2 → 检查 LightAttack 的打断窗口
3. 动画进度 0.2（前摇中）→ 不允计 → InputBuffer 暂存
4. 动画进度 0.75（进入打断窗口）→ 过渡触发 → 切到 Dodge

这就是「普攻前摇不能取消，收招可以取消」的物理基础。

**玩家在普攻中被怪物打到 (Hurt, 优先级 5)**:
1. Hurt 过渡设了 `ForceInterrupt = true`
2. 直接跳过所有优先级和窗口检查 → 立刻切入 Hurt

为什么用 ForceInterrupt？被打还要等收招才能倒地显然不合理。受击必须立刻响应。

#### 4.4 ForceInterrupt 适用场景

- 受击 / 死亡
- 过场动画接管
- 游戏全局状态切换（Pause、GameOver）

---

### Step 5: `State.cs`

运行时状态容器，逻辑简单。

```csharp
public class State
{
    public int Id;
    public string Name;
    public int Priority;           // 优先级，越大越难被打断
    public float Duration;         // 0 = 不定长
    public bool IsInterruptible;   // false = 完全不可打断（霸体 / 闪避无敌帧）
    public float InterruptWindowStart;  // 归一化 0-1
    public float InterruptWindowEnd;
    public Transition[] Transitions;
    public StateMachine SubMachine;     // 子状态机（可选）
    public float ElapsedTime;

    // 生命周期回调（由 Controller 注册）
    public Action<State> OnEnter;
    public Action<State> OnUpdate;
    public Action<State> OnExit;
}
```

#### 5.1 打断窗口判断

```csharp
public bool IsInInterruptWindow(float normalizedTime)
{
    if (!IsInterruptible) return false;      // 霸体：不允许任何打断
    if (Duration <= 0f) return true;          // 不定长状态 (Idle/Move)：始终可打断
    return normalizedTime >= InterruptWindowStart
        && normalizedTime <= InterruptWindowEnd;
}
```

#### 5.2 配置约定

| 用法 | Duration | IsInterruptible | 窗口 |
|------|----------|-----------------|------|
| Idle/Move | 0 | true | 忽略（始终可打断） |
| 攻击 (可取消收招) | 有值 | true | (0.7, 1.0) |
| 霸体攻击 | 有值 | false | 忽略 |
| Dodge 无敌帧 | 有值 | false | 忽略 |

---

### Step 6: `StateMachine.cs`

主引擎，最复杂的类。

#### 6.1 数据结构

```csharp
public class StateMachine
{
    State[] _states;
    State _currentState;
    ParameterStore _parameters;
    Transition[] _globalTransitions;  // AnyState 过渡
    int _defaultStateId;
    int _previousStateId = -1;

    public ParameterStore Parameters => _parameters;
    public State CurrentState => _currentState;
}
```

#### 6.2 Initialize 流程

1. 遍历参数定义 → 创建 `ParameterStore`
2. 遍历状态定义 → 创建 `State[]`:
   - 分配自增 ID
   - 解析 `TransitionDef[]` → `Transition[]`（根据 TargetStateName → TargetStateId）
   - 递归解析 `ConditionDef[]` → `FsmCondition[]`
   - 有 `SubMachineDefinition` → 递归创建子 `StateMachine`
3. 解析全局过渡 → `Transition[]`
4. 解析 `DefaultStateName` → `_defaultStateId`
5. `ChangeState(_defaultStateId)`

#### 6.3 Update — 顺序很关键

```
Update(deltaTime, normalizedTime):

1. _parameters.ResetConsumedTriggers()
2. _currentState.ElapsedTime += deltaTime

3. 先评估全局过渡 (AnyState)
   for each T in _globalTransitions:
     if T.Evaluate(..) → ChangeState(T.TargetStateId) → return

4. 再评估当前状态过渡
   for each T in _currentState.Transitions:
     if T.Evaluate(..) → ChangeState(T.TargetStateId) → return

5. 无过渡 → 更新子状态机
   _currentState.SubMachine?.Update(deltaTime, subNormalizedTime)

6. _currentState.OnUpdate?.Invoke(_currentState)
```

**为什么全局过渡要排前面?**

同一帧内玩家按闪避、怪物命中角色:
- SetTrigger("Dodge") + SetTrigger("Hit") 都在当帧
- 如果先评估当前状态的过渡（Combo 自动推进），可能先切到了 Attack4
- 全局过渡先评估 → Dodge (优先级 7) / Hurt (ForceInterrupt) 优先响应

#### 6.4 ChangeState

```csharp
void ChangeState(int targetId)
{
    // 退出
    _currentState.OnExit?.Invoke(_currentState);
    _currentState.SubMachine?.ExitAll();

    // 切换
    _previousStateId = _currentState.Id;
    _currentState = _states[targetId];

    // 进入
    _currentState.ElapsedTime = 0f;
    _currentState.OnEnter?.Invoke(_currentState);
    _currentState.SubMachine?.EnterDefaultState();
}
```

#### 6.5 一帧最多一次过渡

`Update` 里无论全局过渡还是状态过渡命中，都是 ChangeState → return。这防止 A→B→C→A 的无限循环。

#### 6.6 ForceTransition — 跳过一切直接切入

```csharp
public void ForceTransition(int stateId) => ChangeState(stateId);
```

外部调用，不存在经过条件/优先级/窗口检查。用于死亡、过场。

---

### Step 7-10: 配置层

把上面的运行时字段做成可序列化的 Unity 类。

#### ParameterDef (struct)

```csharp
[System.Serializable]
public struct ParameterDef
{
    public string Name;
    public ParameterType Type;
    public bool DefaultBool;
    public int DefaultInt;
    public float DefaultFloat;
    // Trigger 不需要默认值，始终 false
}
```

#### ConditionDef (class)

```csharp
[System.Serializable]
public class ConditionDef
{
    public ConditionType Type;
    public string ParameterName;     // Bool/Int/Float/Trigger 用
    public CompareOp Comparison;     // Int/Float 用
    public bool BoolValue;
    public int IntValue;
    public float FloatValue;
    public LogicMode LogicMode;      // And/Or 用
    public ConditionDef[] SubConditions; // And/Or/Not 用
    public string CustomTypeName;    // Custom 扩展用
}
```

**工厂方法**: `ConditionDef` → 运行时 `FsmCondition` 的转换:

```
CreateCondition(def, paramStore):
  switch def.Type:
    Bool    → new ConditionBool(paramStore.GetId(def.ParameterName), def.BoolValue)
    Int     → new ConditionInt(paramStore.GetId(def.ParameterName), def.Comparison, def.IntValue)
    Float   → new ConditionFloat(paramStore.GetId(def.ParameterName), def.Comparison, def.FloatValue)
    Trigger → new ConditionTrigger(paramStore.GetId(def.ParameterName))
    Always  → new ConditionAlways()
    Never   → new ConditionNever()
    And     → new ConditionAnd(def.SubConditions 递归创建)
    Or      → new ConditionOr(def.SubConditions 递归创建)
    Not     → new ConditionNot(CreateCondition(def.SubConditions[0]))
    Custom  → ConditionRegistry.Resolve(def)
```

#### TransitionDef

```csharp
[System.Serializable]
public class TransitionDef
{
    public string TargetStateName;      // 用名字引用（初始化时 resolve 成 ID）
    public int PriorityOverride = -1;   // -1 = 继承目标状态的优先级
    public bool CanInterrupt;
    public bool ForceInterrupt;
    public bool HasExitTime;
    public float ExitTime;
    public LogicMode ConditionMode;
    public ConditionDef[] Conditions;
}
```

#### StateDef

```csharp
[System.Serializable]
public class StateDef
{
    public string Name;
    public int Priority;
    public float Duration;
    public bool IsInterruptible = true;
    public float InterruptWindowStart;
    public float InterruptWindowEnd = 1f;
    public StateMachineAsset SubStateMachine;
    public TransitionDef[] Transitions;
}
```

#### StateMachineAsset (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "CombatCore/State Machine")]
public class StateMachineAsset : ScriptableObject
{
    public string MachineName;
    public ParameterDef[] Parameters;
    public StateDef[] States;
    public TransitionDef[] GlobalTransitions;  // AnyState
    public string DefaultStateName;
}
```

---

### Step 11: `StateMachineController.cs`

FSM 在 GameObject 上跑起来的地方。

```csharp
public class StateMachineController : MonoBehaviour
{
    [SerializeField] StateMachineAsset _asset;
    StateMachine _stateMachine;
    Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _stateMachine = new StateMachine();
        _stateMachine.Initialize(_asset);
    }

    void Update()
    {
        float normalizedTime = 0f;
        if (_animator != null)
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            normalizedTime = info.normalizedTime % 1f;
        }
        _stateMachine.Update(Time.deltaTime, normalizedTime);
    }

    // 公共 API
    public void SetBool(string n, bool v)   => _stateMachine.Parameters.SetBool(n, v);
    public void SetFloat(string n, float v) => _stateMachine.Parameters.SetFloat(n, v);
    public void SetInt(string n, int v)     => _stateMachine.Parameters.SetInt(n, v);
    public void SetTrigger(string n)        => _stateMachine.Parameters.SetTrigger(n);
    public State GetCurrentState()          => _stateMachine.CurrentState;
}
```

#### Animator 对接注意点

```csharp
normalizedTime = info.normalizedTime % 1f;
```

Loop 动画的 `normalizedTime` 超过 1.0（第 2 次循环是 2.2），`% 1f` 取归数化余数以用于打断窗口计算。

---

### Step 12: `InputBuffer.cs`

连招手感的灵魂。不放在 FSM 核心引擎里，做独立组件。

**工作原理**:
```
用户按闪避 → 当前在攻击前摇（不在打断窗口）
           → InputBuffer 暂存："Dodge" + 时间戳
           → 每帧检查：当前帧在打断窗口内？
           → 是 → SetTrigger("Dodge") → FSM 正常处理
           → 过期 → 丢弃
```

**缓冲窗口取值参考**:
- 0.05s — 太短，几乎无感
- **0.1-0.15s** — 崩坏3/绝区零大约这个区间
- 0.2-0.3s — 比较宽容

实现:
```csharp
public class InputBuffer
{
    struct BufferedInput { public string ParamName; public float Remaining; }
    BufferedInput? _buffered;
    float _window = 0.15f;

    public void Buffer(string paramName)
        => _buffered = new() { ParamName = paramName, Remaining = _window };

    public void Update(State currentState, ParameterStore parameters, float dt)
    {
        if (_buffered == null) return;
        var b = _buffered.Value;
        b.Remaining -= dt;
        if (b.Remaining <= 0f) { _buffered = null; return; }
        if (currentState.IsInInterruptWindow(/* 当前动画进度 */))
        {
            parameters.SetTrigger(b.ParamName);
            _buffered = null;
        }
        else _buffered = b;
    }

    public void Clear() => _buffered = null;
    public bool HasPending => _buffered != null;
}
```

`CombatStateMachineController`（后续做）会整合 InputBuffer，在 Update 里先释放缓冲、再跑 FSM。

---

## 三、几个核心规则

### 3.1 一帧最多一次过渡

Update 里过渡命中 → ChangeState → return。这防止了 A→B→C 的链式切换。

### 3.2 子状态机的更新时机

只有父级没有过渡发生时，才更新子状态机。父级切走了，子级跟着一起退出。

### 3.3 参数隔离

父状态机和子状态机各有独立的 `ParameterStore`，参数不互通。父级如有需要，通过 OnEnter 回调手动往子级同步。

### 3.4 Trigger 的生命周期

```
SetTrigger → 同帧条件评估可读到 → 读后消耗 → 没被读到 → ResetConsumedTriggers 清掉
```

**Trigger 不过帧存活。** 这是有意为之。

---

## 四、快速自测

做完 ParameterStore 后:
```
store.Initialize(Speed(Float), IsGrounded(Bool), Attack(Trigger))
SetFloat("Speed", 5) → GetFloat("Speed") == 5 ✓
SetTrigger("Attack") → GetTrigger("Attack") == true
                     → GetTrigger("Attack") == false  (消耗验证)
                     → ResetConsumedTriggers()
                     → GetTrigger("Attack") == false  (重置验证)
```

做完 StateMachine 后:
```
创建最小 FSM: Idle(0) ←→ Move(1), 条件: Speed > 0.1
全局过渡: Dodge(7), ForceInterrupt
1. Idle, Speed=0 → 停在 Idle
2. Speed=5 → 切换到 Move
3. Speed=0 → 切回 Idle
4. Move 中, 不改变 Speed, SetTrigger("Dodge") → 立即切换到 Dodge ✓
```

---

## 五、与后续模块对接

做完 FSM 后，按你的规划会用在这里:

- **技能系统 (2.3)**: 每个技能 = 一个状态/子状态机。前摇→判命→后摇→冷却 = Startup→Active→Recovery→Cooldown
- **连招系统 (2.5)**: 连招表 = 「状态 A 的某窗口可过渡到状态 B」，直接是打断窗口 + 过渡的组合
- **敌人 AI (2.6)**: 巡逻→发现→追击→攻击→逃跑 = 一套 FSM 配置，和角色用同一套引擎
