using UnityEngine;

namespace UI.StateEngine {
    /// <summary>
    /// A simple debug notifier that logs lifecycle events to Unity's console.
    /// </summary>
    public class StateEngineDebugNotifier : IStateLifecycleNotifier {
        public void OnStateEnter(StateBase state)
            => Debug.Log($"[StateEngine] <color=cyan>OnEnter</color>  → {state.GetType().Name}");

        public void OnStatePause(StateBase state)
            => Debug.Log($"[StateEngine] <color=yellow>OnPause</color>  → {state.GetType().Name}");

        public void OnStateResume(StateBase state)
            => Debug.Log($"[StateEngine] <color=green>OnResume</color> → {state.GetType().Name}");

        public void OnStateExit(StateBase state)
            => Debug.Log($"[StateEngine] <color=red>OnExit</color>   → {state.GetType().Name}");
    }
}