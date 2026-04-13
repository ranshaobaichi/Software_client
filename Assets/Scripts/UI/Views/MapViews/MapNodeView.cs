using UnityEngine;
using UnityEngine.UI;
using Constants;

namespace UI.Views {
    public class MapNodeView : MonoBehaviour {
        [SerializeField]
        private Image _icon;

        private ServerMapNode m_data;

        public void Init(ServerMapNode data) {
            m_data = data;

            switch (data.type) {
                case NodeType.NORMAL:
                    _icon.color = Color.white;
                    break;
                case NodeType.ELITE:
                    _icon.color = Color.red;
                    break;
                case NodeType.EVENT:
                    _icon.color = Color.green;
                    break;
                case NodeType.BOSS:
                    _icon.color = Color.yellow;
                    break;
            }
        }

        public void ConnectTo(MapNodeView target) {
            Debug.DrawLine(transform.position, target.transform.position, Color.white, 10f);
        }
    }
}