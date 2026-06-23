namespace CombatCore.Core
{
    public class StateMachine
    {
        protected BaseState currentState;

        // 两种方式  
        // 一种 实例化 即用即销：new State()
        public void ChangeState(BaseState newState)
        {
            currentState?.Exit();
            currentState = newState;
            currentState.Enter();
        }
        public void HandleInput()
        {
            currentState?.HandleInput();
        }
        public void Update()
        {
            currentState?.Update();
        }
        public void PhysicsUpdate()
        {
            currentState?.PhysicsUpdate();
        }
    } 
}