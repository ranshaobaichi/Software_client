using System;

namespace Network {
    public interface INetworkChannel {
        bool IsConnected { get; }
        void Connect();
        void Disconnect();
        void Send<T>(T payload) where T : class;
        void RegisterHandler<T>(Action<T> callback) where T : class;
        void ClearHandler();
        void DispatchPendingMessages();
        event Action OnConnected;
        event Action OnDisconnected;
    }
}