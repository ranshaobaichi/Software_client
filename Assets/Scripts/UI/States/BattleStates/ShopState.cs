using UnityEngine;
using System;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using UI.StateEngine;
using UI.ViewModels;

public class ShopState : StateBase {
    private INetworkChannel m_channel;
    public ShopViewModel vm;

    private string MyUid => PlayerData.SInstance.basicInfo.uid;

    protected override void OnEnter() {
        base.OnEnter();

        vm = new ShopViewModel();
        vm.MyUid = MyUid;

        var mainHandler = new Dictionary<ShopResponseType, LongConnectionMainDispatchEntry> {
                {
                        ShopResponseType.SHOP_SYNC,
                        NetworkManager.CreateDispatchEntry<ShopSyncResponse>(OnSync)
                }
        };

        var pushHandler = new Dictionary<int, Action>();

        m_channel = NetworkManager.SInstance.CreateLongConnection(
                NetworkConstants.DefaultHost,
                NetworkConstants.ShopPort,
                mainHandler,
                pushHandler
        );
    }


    protected override void OnResume() {
        base.OnResume();

        m_channel?.Connect();

#if UNITY_EDITOR
        Application.runInBackground = true;
#endif

        SendInit();
    }

    protected override void OnPause() {
        base.OnPause();

        m_channel?.Disconnect();
    }

    protected override void OnExit() {
        base.OnExit();

        NetworkManager.SInstance.RemoveConnection(m_channel);
        m_channel = null;
    }
    
    public void TEST_SwitchToMap() {
        m_StateEngine.AddTop<MapState>();
    }
    
    #region ===== 发送 =====

    public void SendInit() {
        var req = new ShopInitRequest {
                type = (int)ShopRequestType.SHOP_INIT,
                uid = MyUid
        };

        m_channel.Send(req);
    }

    public void Buy(string itemId) {
        var req = new ShopBuyRequest {
                type = (int)ShopRequestType.SHOP_BUY_ITEM,
                uid = MyUid,
                itemId = itemId
        };

        m_channel.Send(req);
    }

    public void Move(string itemId) {
        var req = new ShopMoveCursorRequest {
                type = (int)ShopRequestType.SHOP_MOVE_CURSOR,
                uid = MyUid,
                itemId = itemId
        };

        m_channel.Send(req);
    }

    #endregion

    #region ===== 接收 =====

    private void OnSync(ShopSyncResponse res) {
        vm.ApplySync(res.items);
        vm.ApplyPlayers(res.playerInfos);
    }

    #endregion
}