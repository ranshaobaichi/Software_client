using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace UI.Views {
    public class ShopItemUI : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler {
        [SerializeField]
        private Text _itemIdText;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private Button _button;

        public event Action<string> Clicked;
        public event Action<string> Hovered;

        private string _itemId;

        private bool m_taken;
        private bool m_selectedByOthers;
        private bool m_selectedByMe;
        private bool m_isHover;


        public void SetData(string id, bool taken, bool others, bool me) {
            _itemId = id;

            m_taken = taken;
            m_selectedByOthers = others;
            m_selectedByMe = me;

            _itemIdText.text = id;

            _button.interactable = !taken && !others;

            Refresh();
        }

        private void Awake() {
            _button.onClick.AddListener(() => { Clicked?.Invoke(_itemId); });
        }

        public void OnPointerEnter(PointerEventData eventData) {
            m_isHover = true;

            Hovered?.Invoke(_itemId);

            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData) {
            m_isHover = false;

            Hovered?.Invoke(null);

            Refresh();
        }

        private void Refresh() {
            if (m_taken) {
                _background.color = Color.black;
                return;
            }


            if (m_selectedByMe) {
                _background.color = Color.cyan;
                return;
            }


            if (m_selectedByOthers) {
                _background.color = Color.green;
                return;
            }


            if (m_isHover) {
                _background.color = new Color(0.9f, 0.9f, 0.9f);
                return;
            }

            _background.color = Color.white;
        }
    }
}