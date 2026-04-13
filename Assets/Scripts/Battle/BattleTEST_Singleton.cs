using UnityEngine;

namespace Battle {
    public class BattleTEST_Singleton : MonoBehaviour {
        public void TEST_SwitchToHome() {
            GameSceneManager.SInstance.SwitchScene(Constants.SceneType.HOME);
        }
    }
}