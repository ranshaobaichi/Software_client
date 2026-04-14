using UnityEngine;
using System;
using System.Collections.Generic;
using Network.Messages;

namespace Network {
    /// <summary>
    /// Long-connection inbound pipeline: probe <see cref="LongConnectionProbeEnvelope"/>,
    /// typed <see cref="LongEnvelope{T}"/> for <c>data</c>, then push markers.
    /// </summary>
    public static class LongConnectionInboundDispatcher {
        public static void Dispatch(byte[] raw, IMessageSerializer serializer, LongConnectionDispatchTables tables) {
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
            // Reuse probe list for push markers; typed envelope may supply a different list (no copy unless consumed code mutates).
            List<int> markersForPush = probe.pushMessages;

            bool mainKnown = tables.MainByTypeInt.TryGetValue(mainType, out LongConnectionMainDispatchEntry entry);
            if (!mainKnown) {
                Debug.LogWarning($"[LongConnectionInboundDispatcher] Unknown main type {mainType}");
            }

            if (mainKnown) {
                if (entry.EnvelopeClrType != null) {
                    try {
                        object typed = serializer.Deserialize(raw, entry.EnvelopeClrType);
                        object data = entry.DataField?.GetValue(typed);
                        try {
                            entry.Handler.Invoke(data);
                        } catch (Exception ex) {
                            Debug.LogWarning("[LongConnectionInboundDispatcher] Main handler threw: " + ex.Message);
                        }

                        if (entry.PushMessagesField?.GetValue(typed) is List<int> fromTyped)
                            markersForPush = fromTyped;
                    } catch (Exception ex) {
                        Debug.LogWarning("[LongConnectionInboundDispatcher] Typed envelope deserialize failed: " +
                                         ex.Message);
                    }
                }
            } else if (!tables.DispatchPushWhenMainTypeUnknown) {
                return;
            }

            if (markersForPush != null) {
                foreach (int marker in markersForPush) {
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
        }
    }
}