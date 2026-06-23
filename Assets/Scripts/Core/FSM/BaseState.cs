namespace CombatCore.Core
{
    // 抽象类 + 虚函数 实现
    public abstract class BaseState : IState
    {
        // 虚函数不要求子类去实现 保留最低的调用 可能会遗忘重写
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() { }
        public virtual void PhysicsUpdate() { }
        public virtual void HandleInput() { }

        // 抽象函数 要求子类必须去实现 扩展比较麻烦 子类都需要实现(感觉和接口有一拼了)
        // public abstract void Enter();
    }
}