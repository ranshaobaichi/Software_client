using System;

namespace UI.StateEngine {
    public interface IStateEngine {
        bool isEmpty { get; }
        int count { get; }

        /// <summary>Top State | Null</summary>
        StateBase Peek();

        bool Contains<T>() where T : StateBase;

        /// <summary>编译期已知类型时使用。</summary>
        bool AddTop<T>() where T : StateBase;

        bool AddTop(Type stateType);

        bool TryRemoveTop();
        bool ReplaceTop<T>() where T : StateBase;
        bool TryRemoveTo<T>() where T : StateBase;

        void Clear();

        void SendMessage<T1, T2>(object message)
                where T1 : StateBase where T2 : StateBase;
    }
}