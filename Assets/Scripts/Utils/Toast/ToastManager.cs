using UnityEngine;

namespace Utils {
    public class ToastManager : MonoBehaviour {
        #region Singleton
        private static ToastManager s_instance;

        public static ToastManager SInstance {
            get {
                if (s_instance == null) {
                    s_instance = FindObjectOfType<ToastManager>();
                    if (s_instance == null) {
                        Debug.LogError("[ToastManager] No ToastManager found");
                    }
                }

                return s_instance;
            }
        }
        #endregion

        private void Awake() {
            if (s_instance != null && s_instance != this) {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }
        
        [SerializeField]
        private GameObject _toastPrefab;
        [SerializeField]
        private RectTransform _toastParent;

        public void ShowToast(string text) {
            var go = Instantiate(_toastPrefab, _toastParent);
            go.transform.SetAsFirstSibling();
            var toast = go.GetComponentInChildren<Toast>();
            toast.SetText(text);
            toast.MoveIn();
        }
    }
}