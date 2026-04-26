using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using Constants;
using Network.Messages;

namespace Network {
    /// <summary>
    /// Long-connection inbound pipeline: probe <see cref="LongConnectionProbeEnvelope"/>,
    /// typed <see cref="LongEnvelope{T}"/> for <c>data</c>, then push markers.
    /// </summary>
    public static class LongConnectionInboundDispatcher {
        public static void Dispatch(byte[] raw, IMessageSerializer serializer, LongConnectionDispatchTables tables,
                int port = -1) {
            if (raw == null || raw.Length == 0) return;
            if (serializer == null || tables == null) return;

            LongConnectionProbeEnvelope probe;
            try {
                probe = serializer.Deserialize(raw, typeof(LongConnectionProbeEnvelope)) as LongConnectionProbeEnvelope;
            } catch (Exception ex) {
                Debug.LogWarning("[LongConnectionInboundDispatcher] Probe deserialize failed: " + ex.Message);
                return;
            }

            if (probe == null) {
                Debug.LogWarning("[LongConnectionInboundDispatcher] Probe deserialize returned null");
                return;
            }

            int mainType = probe.type;
            List<int> markersForPush = probe.pushMessages;
            LogEnvelopeWire(port, mainType, raw, markersForPush);

            bool mainKnown = tables.MainByTypeInt.TryGetValue(mainType, out LongConnectionMainDispatchEntry entry);
            if (!mainKnown) {
                Debug.LogWarning($"[LongConnectionInboundDispatcher] Unknown main type {mainType}");
            }

            if (markersForPush != null) {
                foreach (int marker in markersForPush) {
                    NetworkPacketLog.LogRecvPush(port, marker);
                    if (!tables.PushByMarker.TryGetValue(marker, out Action pushHandler)) {
                        Debug.LogWarning($"[LongConnectionInboundDispatcher] Unknown push marker {marker}");
                        continue;
                    }

                    try {
                        pushHandler?.Invoke();
                    } catch (Exception ex) {
                        Debug.LogWarning("[LongConnectionInboundDispatcher] Push handler threw: " + ex.Message);
                    }
                }
            }

            if (mainKnown) {
                if (entry.EnvelopeClrType != null) {
                    try {
                        object typed = serializer.Deserialize(raw, entry.EnvelopeClrType);
                        object data = entry.DataField?.GetValue(typed);
                        LogParsedData(port, mainType, data);

                        try {
                            entry.Handler.Invoke(data);
                        } catch (Exception ex) {
                            Debug.LogWarning("[LongConnectionInboundDispatcher] Main handler threw: " + ex.Message);
                        }

                        if (entry.PushMessagesField?.GetValue(typed) is List<int> fromTyped) {
                            markersForPush = fromTyped;
                        }
                    } catch (Exception ex) {
                        Debug.LogWarning("[LongConnectionInboundDispatcher] Typed envelope deserialize failed: " +
                                         ex.Message);
                    }
                }
            } else if (!tables.DispatchPushWhenMainTypeUnknown) {
                return;
            }
        }

        private static void LogEnvelopeWire(int port, int mainType, byte[] raw, List<int> markersForPush) {
            if (port != NetworkConstants.BattlePort) {
                return;
            }

            var envelopeJson = NetworkPacketLog.TrimWireText(Encoding.UTF8.GetString(raw));
            var pushSummary = FormatPushSummary(markersForPush);
            NetworkPacketLog.LogRecvEnvelope(port, mainType, envelopeJson, pushSummary);
        }

        private static void LogParsedData(int port, int mainType, object data) {
            if (data == null) {
                return;
            }

            var dataJson = JsonUtility.ToJson(data);
            if (string.IsNullOrEmpty(dataJson) || dataJson == "{}") {
                return;
            }

            NetworkPacketLog.LogRecvParsed(port, mainType, data.GetType().Name, dataJson);
        }

        private static string FormatPushSummary(List<int> markers) {
            if (markers == null || markers.Count == 0) {
                return string.Empty;
            }

            return string.Join(",", markers);
        }
    }
}
