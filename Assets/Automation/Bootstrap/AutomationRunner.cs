using System.Collections;
using UnityEngine;

namespace Automation.Bootstrap {
    /// <summary>
    /// Coroutine host for Automation sessions outside UI MonoBehaviours.
    /// </summary>
    public class AutomationRunner : MonoBehaviour {
        static AutomationRunner s_instance;

        public static AutomationRunner Instance {
            get {
                if (s_instance != null)
                    return s_instance;

                s_instance = FindObjectOfType<AutomationRunner>();
                if (s_instance != null)
                    return s_instance;

                var go = new GameObject("[AutomationRunner]");
                s_instance = go.AddComponent<AutomationRunner>();
                DontDestroyOnLoad(go);
                return s_instance;
            }
        }

        public Coroutine Run(IEnumerator routine) {
            if (routine == null)
                return null;
            return StartCoroutine(routine);
        }

        public void RunAndForget(IEnumerator routine) {
            if (routine != null)
                StartCoroutine(routine);
        }
    }
}
