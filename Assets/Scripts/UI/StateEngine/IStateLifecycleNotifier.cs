namespace UI.StateEngine {
    public interface IStateLifecycleNotifier {
        void OnStateEnter(StateBase state);
        void OnStatePause(StateBase state);
        void OnStateResume(StateBase state);
        void OnStateExit(StateBase state);
    }
}