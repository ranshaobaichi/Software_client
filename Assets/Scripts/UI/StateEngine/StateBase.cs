using UnityEngine;
using System;
using System.Collections.Generic;

namespace UI.StateEngine {
    /// <summary>
    /// State's abstract base
    /// </summary>
    public abstract class StateBase : MonoBehaviour {
        protected IStateEngine m_StateEngine;

        internal void SetStateEngine(IStateEngine stateEngine) => m_StateEngine = stateEngine;
        public virtual void ReceiveMessage(Dictionary<Type, object> messages) { }

        #region Lifecycle Api
        internal void DoEnter() => OnEnter();
        internal void DoPause() => OnPause();
        internal void DoResume() => OnResume();
        internal void DoExit() => OnExit();
        #endregion

        #region Lifecycle Hooks
        protected virtual void OnEnter() { }
        protected virtual void OnResume() { }
        protected virtual void OnPause() { }
        protected virtual void OnExit() { }
        #endregion
    }
}