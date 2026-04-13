using UnityEngine;
using UnityEngine.UI;
using System;
using Constants;
using Network.Messages;

namespace UI.Views {
    public class MapNodeView : MonoBehaviour {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Image _selectImg;

        [SerializeField]
        private Button _button;

        private int m_nodeId;
        private Action<int> m_onClick;
        private void Awake() {
            if (_icon == null)
                _icon = transform.Find("Icon")?.GetComponent<Image>();

            if (_selectImg == null)
                _selectImg = transform.Find("SelectImg")?.GetComponent<Image>();

            if (_button == null)
                _button = GetComponent<Button>();

            if (_button == null)
                _button = GetComponentInChildren<Button>();

            if (_icon == null || _selectImg == null || _button == null) {
                Debug.LogError($"MapNodeView prefab is broken on {gameObject.name}");
            }
        }
        public void Init(MapNode data, Action<int> onClick) {
            m_nodeId = data.nodeId;
            m_onClick = onClick;


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

            ClearSelect();

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }

        private void OnClick() {
            Debug.Log($"Enter battle from node: {m_nodeId}");
            m_onClick?.Invoke(m_nodeId);
        }

        public void SetSelfSelected() {
            _selectImg.gameObject.SetActive(true);
            _selectImg.color = Color.blue;
        }

        public void SetOtherSelected() {
            _selectImg.gameObject.SetActive(true);
            _selectImg.color = Color.red;
        }

        public void ClearSelect() {
            _selectImg.gameObject.SetActive(false);
        }
    }
}