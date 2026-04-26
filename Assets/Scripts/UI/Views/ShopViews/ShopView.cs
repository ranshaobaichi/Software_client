using System.Collections.Generic;
using Network.Messages;
using UnityEngine;
using UI.ViewModels;
using UI.Models;

namespace UI.Views {
    public class ShopView : ViewBase<ShopViewModel> {
        [Header("Prefab & Container")]
        [SerializeField]
        private ShopItemUI _itemPrefab;

        [SerializeField]
        private Transform _container;

        [SerializeField]
        private ShopState _shopState;

        private ShopViewModel m_vm;


        private List<ShopItemUI> m_itemViews = new();

        private void Start() {
            m_vm = _shopState.vm;

            m_vm.Changed += Refresh;
        }


        private void OnItemClick(string itemId) {
            if (!m_vm.CanBuy(itemId))
                return;

            _shopState.Buy(itemId);
        }


        private void OnItemHover(string itemId) {
            if (string.IsNullOrEmpty(itemId)) {
                _shopState.Move("-1");
            } else {
                _shopState.Move(itemId);
            }
        }

        private void EnsureItemCount(int count) {
            while (m_itemViews.Count < count) {
                var view = Instantiate(_itemPrefab, _container);

                view.Clicked += OnItemClick;
                view.Hovered += OnItemHover;

                m_itemViews.Add(view);
            }


            for (int i = 0; i < m_itemViews.Count; i++) {
                m_itemViews[i].gameObject.SetActive(i < count);
            }
        }

        private void Refresh() {
            if (m_vm.Items == null) return;

            EnsureItemCount(m_vm.Items.Count);

            for (int i = 0; i < m_vm.Items.Count; i++) {
                var item = m_vm.Items[i];

                m_itemViews[i].SetData(
                        item.itemId,
                        item.itemStatus == status.buy,
                        m_vm.IsSelectedByOthers(item.itemId),
                        m_vm.IsSelectedByMe(item.itemId)
                );
            }
        }
    }
}