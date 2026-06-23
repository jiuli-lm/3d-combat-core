namespace CombatCore.Core
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Update();
        void PhysicsUpdate();
        void HandleInput();
    }
}