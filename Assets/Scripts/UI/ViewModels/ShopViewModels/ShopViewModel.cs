using System;
using System.Collections.Generic;
using Network.Messages;
using UnityEngine;

namespace UI.ViewModels {
    public class ShopViewModel : ViewModelBase<ShopViewModel> {
        public List<ShopItem> Items { get; private set; }
        public List<playerInfos> Players { get; private set; }

        public string MyUid { get; set; }


        public event Action Changed;


        public void ApplySync(List<ShopItem> items) {
            Items = items;
            NotifyChanged();
        }

        public void ApplyPlayers(List<playerInfos> infos) {
            Players = infos;
            NotifyChanged();
        }

        private void NotifyChanged() {
            Changed?.Invoke();
        }


        public ShopItem GetItem(string itemId) {
            return Items?.Find(x => x.itemId == itemId);
        }

        public bool IsSelectedByMe(string itemId) {
            var item = GetItem(itemId);
            return item != null &&
                   item.itemStatus == status.select &&
                   item.selectUid == MyUid;
        }

        public bool IsSelectedByOthers(string itemId) {
            var item = GetItem(itemId);
            return item != null &&
                   item.itemStatus == status.select &&
                   item.selectUid != MyUid;
        }

        public bool CanBuy(string itemId) {
            var item = GetItem(itemId);
            if (item == null) return false;

            if (item.itemStatus == status.buy) return false;
            if (IsSelectedByOthers(itemId)) return false;

            return true;
        }
    }
}