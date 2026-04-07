using UnityEngine;
using UI.StateEngine;
using UI.States;
using UI.ViewModels;

namespace UI.Views {
    public class HomeButtonAreaView : MonoBehaviour {
        private HomeButtonAreaViewModel m_areaViewModel;
        private UIStateFinder m_stateFinder;
        
        public void SetViewModel(HomeButtonAreaViewModel areaViewModel) {
            m_areaViewModel = areaViewModel;
        }

        #region Button Callbacks
        public void OnQuitGameClicked() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnSwitchOnlineLobbyClicked() {
            m_stateFinder.Current(this).AddTop<OnlineLobbyState>();
        }
        #endregion
    }
}