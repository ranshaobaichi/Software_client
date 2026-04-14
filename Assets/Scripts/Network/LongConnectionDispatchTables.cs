using System;
using System.Collections.Generic;
using System.Reflection;
using Network.Messages;

namespace Network {
    /// <summary>
    /// Maps a long-connection outer <c>type</c> (wire int) to the CLR type of <see cref="Messages.LongEnvelope{T}.data"/>
    /// and a handler invoked with that <c>data</c> after a typed second deserialize.
    /// </summary>
    public sealed class LongConnectionMainDispatchEntry {
        private const BindingFlags EnvelopeFieldFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public Type DataClrType { get; }
        public Action<object> Handler { get; }

        /// <summary>
        /// Cached <see cref="LongEnvelope{T}"/> closed over <see cref="DataClrType"/>; null if construction failed.
        /// </summary>
        internal Type EnvelopeClrType { get; }

        internal FieldInfo DataField { get; }
        internal FieldInfo PushMessagesField { get; }

        public LongConnectionMainDispatchEntry(Type dataClrType, Action<object> handler) {
            DataClrType = dataClrType ?? throw new ArgumentNullException(nameof(dataClrType));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));

            Type envelopeClr = null;
            try {
                envelopeClr = typeof(LongEnvelope<>).MakeGenericType(DataClrType);
            } catch (Exception ex) {
                UnityEngine.Debug.LogWarning(
                        $"[LongConnectionMainDispatchEntry] MakeGenericType failed for {DataClrType.FullName}: {ex.Message}");
            }

            EnvelopeClrType = envelopeClr;
            if (envelopeClr != null) {
                DataField = envelopeClr.GetField("data", EnvelopeFieldFlags);
                PushMessagesField = envelopeClr.GetField("pushMessages", EnvelopeFieldFlags);
            }
        }
    }

    /// <summary>
    /// Immutable snapshot of main-type handlers and <see cref="Messages.LongEnvelope{T}.pushMessages"/> marker handlers,
    /// bound when the <see cref="TcpConnectionChannel"/> is constructed.
    /// </summary>
    public sealed class LongConnectionDispatchTables {
        private readonly Dictionary<int, LongConnectionMainDispatchEntry> m_mainByTypeInt;
        private readonly Dictionary<int, Action> m_pushByMarker;

        /// <summary>
        /// When true (default), <c>pushMessages</c> are still dispatched if the outer <c>type</c> is unknown to <see cref="MainByTypeInt"/>.
        /// </summary>
        public bool DispatchPushWhenMainTypeUnknown { get; }

        public IReadOnlyDictionary<int, LongConnectionMainDispatchEntry> MainByTypeInt => m_mainByTypeInt;
        public IReadOnlyDictionary<int, Action> PushByMarker => m_pushByMarker;

        public LongConnectionDispatchTables(
                IReadOnlyDictionary<int, LongConnectionMainDispatchEntry> mainByTypeInt,
                IReadOnlyDictionary<int, Action> pushByMarker,
                bool dispatchPushWhenMainTypeUnknown = true
        ) {
            if (mainByTypeInt == null) throw new ArgumentNullException(nameof(mainByTypeInt));
            if (pushByMarker == null) throw new ArgumentNullException(nameof(pushByMarker));
            m_mainByTypeInt = new Dictionary<int, LongConnectionMainDispatchEntry>(mainByTypeInt);
            m_pushByMarker = new Dictionary<int, Action>(pushByMarker);
            this.DispatchPushWhenMainTypeUnknown = dispatchPushWhenMainTypeUnknown;
        }
    }
}
