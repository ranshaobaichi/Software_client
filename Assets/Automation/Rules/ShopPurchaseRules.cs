using Network.Messages;

namespace Automation.Rules {
    public static class ShopPurchaseRules {
        public static bool CanBuy(ShopItem item, string myUid) {
            if (item == null)
                return false;
            if (item.itemStatus == status.buy)
                return false;
            if (item.itemStatus == status.select && item.selectUid != myUid && !string.IsNullOrEmpty(item.selectUid))
                return false;
            return true;
        }

        public static ShopItem FindFirstAffordable(System.Collections.Generic.List<ShopItem> items, string myUid) {
            if (items == null)
                return null;
            for (var i = 0; i < items.Count; i++) {
                ShopItem item = items[i];
                if (CanBuy(item, myUid))
                    return item;
            }

            return null;
        }
    }
}
