using System;
using System.Collections.Generic;

namespace Network.Messages {
    /// <summary>
    /// 虚构商店模块协议草案（尚未接入业务代码，仅供文档对齐 / 联调示例）。
    /// 与 <see cref="ServerNetworkMessages"/> 中现有 Shop Service 并存，字段命名保持 camelCase。
    /// </summary>

    #region Draft Opcodes

    public enum ShopDraftRequestType {
        CATALOG_QUERY = 10,
        SHELF_REFRESH = 11,
        RESERVE_ITEM = 12,
        PURCHASE = 13,
        CANCEL_RESERVE = 14,
        WALLET_QUERY = 15,
        APPLY_COUPON = 16,
    }

    public enum ShopDraftResponseType {
        CATALOG_SYNC = 10,
        SHELF_REFRESH_RESULT = 11,
        RESERVE_RESULT = 12,
        PURCHASE_RESULT = 13,
        WALLET_SYNC = 14,
        COUPON_APPLY_RESULT = 15,
        LIMITED_OFFER_PUSH = 16,
    }

    #endregion

    #region Draft Structures

    public enum ShopCurrencyType {
        gold = 0,
        gem = 1,
        eventToken = 2,
    }

    public enum ShopItemRarity {
        common = 0,
        rare = 1,
        epic = 2,
        legendary = 3,
    }

    public enum ShopShelfSlotState {
        available = 0,
        reserved = 1,
        soldOut = 2,
        locked = 3,
    }

    [Serializable]
    public class ShopCurrencyBalance {
        public ShopCurrencyType currencyType;
        public int amount;
    }

    [Serializable]
    public class ShopDiscountRule {
        public string couponCode;
        public int discountPercent;
        public ShopCurrencyType currencyType;
        public long expireAtUnix;
    }

    [Serializable]
    public class ShopCatalogItem {
        public string itemId;
        public string displayName;
        public string description;
        public ShopItemRarity rarity;
        public ShopCurrencyType priceCurrency;
        public int basePrice;
        public int stock;
        public string categoryId;
        public List<string> tagIds;
    }

    [Serializable]
    public class ShopShelfSlot {
        public int slotIndex;
        public string itemId;
        public ShopShelfSlotState slotState;
        public string reservedUid;
        public long reservedUntilUnix;
    }

    [Serializable]
    public class ShopLimitedOffer {
        public string offerId;
        public string itemId;
        public int discountedPrice;
        public ShopCurrencyType currencyType;
        public long startAtUnix;
        public long endAtUnix;
        public int maxPurchasePerPlayer;
    }

    [Serializable]
    public class ShopPlayerSnapshot {
        public PlayerData.PlayerBasicInfo playerInfo;
        public List<ShopCurrencyBalance> wallet;
        public List<string> ownedItemIds;
        public List<string> activeCouponCodes;
    }

    [Serializable]
    public class ShopPurchaseReceipt {
        public string transactionId;
        public string itemId;
        public int paidAmount;
        public ShopCurrencyType currencyType;
        public long purchasedAtUnix;
    }

    #endregion

    #region Draft Requests

    [Serializable]
    public class ShopCatalogQueryRequest : ClientNetworkMessage {
        public string uid;
        public string categoryId;
        public bool includeLimitedOffers;
    }

    [Serializable]
    public class ShopShelfRefreshRequest : ClientNetworkMessage {
        public string uid;
        public ShopCurrencyType payCurrency;
    }

    [Serializable]
    public class ShopReserveItemRequest : ClientNetworkMessage {
        public string uid;
        public int slotIndex;
        public string itemId;
    }

    [Serializable]
    public class ShopPurchaseItemRequest : ClientNetworkMessage {
        public string uid;
        public string itemId;
        public int slotIndex;
        public string couponCode;
    }

    [Serializable]
    public class ShopCancelReserveRequest : ClientNetworkMessage {
        public string uid;
        public int slotIndex;
    }

    [Serializable]
    public class ShopWalletQueryRequest : ClientNetworkMessage {
        public string uid;
    }

    [Serializable]
    public class ShopApplyCouponRequest : ClientNetworkMessage {
        public string uid;
        public string couponCode;
    }

    #endregion

    #region Draft Responses

    [Serializable]
    public class ShopCatalogSyncResponse : ServerNetworkMessages {
        public List<ShopCatalogItem> catalog;
        public List<ShopShelfSlot> shelf;
        public List<ShopLimitedOffer> limitedOffers;
        public List<ShopPlayerSnapshot> players;
        public int refreshCost;
        public ShopCurrencyType refreshCurrency;
    }

    [Serializable]
    public class ShopShelfRefreshResultResponse : ServerNetworkMessages {
        public List<ShopShelfSlot> shelf;
        public List<ShopCurrencyBalance> wallet;
        public int nextRefreshCost;
    }

    [Serializable]
    public class ShopReserveResultResponse : ServerNetworkMessages {
        public ShopShelfSlot slot;
        public bool success;
        public string reason;
    }

    [Serializable]
    public class ShopPurchaseResultResponse : ServerNetworkMessages {
        public bool success;
        public string reason;
        public ShopPurchaseReceipt receipt;
        public ShopShelfSlot slot;
        public List<ShopCurrencyBalance> wallet;
        public List<string> ownedItemIds;
    }

    [Serializable]
    public class ShopWalletSyncResponse : ServerNetworkMessages {
        public List<ShopCurrencyBalance> wallet;
        public List<ShopDiscountRule> activeCoupons;
    }

    [Serializable]
    public class ShopCouponApplyResultResponse : ServerNetworkMessages {
        public bool success;
        public string reason;
        public ShopDiscountRule appliedRule;
    }

    [Serializable]
    public class ShopLimitedOfferPushResponse : ServerNetworkMessages {
        public ShopLimitedOffer offer;
        public long serverTimeUnix;
    }

    #endregion
}
