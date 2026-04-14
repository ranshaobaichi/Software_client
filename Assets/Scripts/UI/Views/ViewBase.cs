using UnityEngine;
using System.ComponentModel;
using UI.ViewModels;

namespace UI.Views {
    public abstract class ViewBase<TVm> : MonoBehaviour where TVm : ViewModelBase<TVm> {
        protected TVm ViewModel { get; private set; }
        private bool m_isInitialized = false;

        /// <summary>
        /// Set or change the ViewModel.
        /// The view will subscribe to the new ViewModel and call <see cref="Render"/>.
        /// If there is an old ViewModel, it will be unsubscribed first.
        /// </summary>
        public void SetViewModel(TVm vm) {
            Unsubscribe();
            ViewModel = vm;
            if (ViewModel == null) {
                return;
            }

            ViewModel.PropertyChanged += OnRender;
            OnRender(null, null);
        }

        private void OnRender(object sender, PropertyChangedEventArgs e) {
            if (ViewModel == null) {
                Debug.LogError($"[{gameObject.name}] ViewModel is null. Cannot render view.");
                return;
            }

            InitIfNot();
            Render();
        }

        private void Unsubscribe() {
            if (ViewModel != null) {
                ViewModel.PropertyChanged -= OnRender;
                ViewModel = null;
            }
        }

        private void OnDestroy() { Unsubscribe(); }

        private void InitIfNot() {
            if (m_isInitialized) {
                return;
            }

            Init();
            m_isInitialized = true;
        }

        protected virtual void Render() { }
        protected virtual void Init() { }
    }
}