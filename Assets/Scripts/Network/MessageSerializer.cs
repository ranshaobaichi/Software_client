using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Reflection;
using Network.Messages;

namespace Network {
    public interface IMessageSerializer {
        byte[] Serialize<T>(T payload) where T : class;
        object Deserialize(byte[] data, Type type);
    }
    
    /// <summary>
    /// Serializer based on Unity's built-in JsonUtility.
    /// Note that JsonUtility has limitations
    /// (e.g. no support for polymorphism, dictionaries, etc.) and is not recommended for complex scenarios;
    /// consider using a more robust JSON library if needed.
    /// </summary>
    public class JsonMessageSerializer : IMessageSerializer {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        /// <summary>
        /// Per-<see cref="Type"/> deserialize shape: built once so hot-path avoids repeated
        /// <see cref="GetInstanceField"/> / <see cref="IsMarkerStylePayloadType"/> and skips
        /// <see cref="JsonHasEmptyObjectForKey"/> when no <c>data</c> field or no marker payload.
        /// </summary>
        private sealed class DeserializeShape {
            public readonly bool RootMarkerSynthesis;
            public readonly FieldInfo DataField;
            public readonly bool DataFieldPayloadIsMarkerStyle;

            public DeserializeShape(bool rootMarkerSynthesis, FieldInfo dataField, bool dataFieldPayloadIsMarkerStyle) {
                RootMarkerSynthesis = rootMarkerSynthesis;
                DataField = dataField;
                DataFieldPayloadIsMarkerStyle = dataFieldPayloadIsMarkerStyle;
            }
        }

        private static readonly ConcurrentDictionary<Type, DeserializeShape> DeserializeShapes =
                new ConcurrentDictionary<Type, DeserializeShape>();

        private static DeserializeShape BuildDeserializeShape(Type type) {
            FieldInfo dataField = GetInstanceField(type, "data");
            bool dataMarker = dataField != null && IsMarkerStylePayloadType(dataField.FieldType);
            return new DeserializeShape(IsMarkerStylePayloadType(type), dataField, dataMarker);
        }

        public byte[] Serialize<T>(T payload) where T : class {
            if (payload == null) return null;
            string json = JsonUtility.ToJson(payload);
            return json == null ? null : Utf8.GetBytes(json);
        }

        public object Deserialize(byte[] data, Type type) {
            if (data == null || data.Length == 0 || type == null) return null;
            string json = Utf8.GetString(data).Trim();
            json = StripUtf8Bom(json);
            if (string.IsNullOrEmpty(json)) return null;

            DeserializeShape shape = DeserializeShapes.GetOrAdd(type, BuildDeserializeShape);
            object deserialized = JsonUtility.FromJson(json, type);
            if (deserialized == null && IsEmptyJsonObject(json) && shape.RootMarkerSynthesis)
                deserialized = TryCreate(type);

            if (shape.DataField != null && deserialized != null && shape.DataFieldPayloadIsMarkerStyle) {
                if (shape.DataField.GetValue(deserialized) == null && JsonHasEmptyObjectForKey(json, "data")) {
                    object emptyInstance = TryCreate(shape.DataField.FieldType);
                    if (emptyInstance != null)
                        shape.DataField.SetValue(deserialized, emptyInstance);
                }
            }

            NormalizeBroadcastRoomStatusUnityArtifacts(deserialized, type);

            return deserialized;
        }

        /// <summary>
        /// <see cref="JsonUtility"/> expands <c>null</c> reference fields into fully-elided default objects on the wire
        /// (e.g. <c>roomInfo</c> becomes <c>{"roomId":0,...}</c>). After <see cref="JsonUtility.FromJson"/> that reads as a
        /// non-null <see cref="RoomInfo"/> even though the sender meant absence of data—normalize back to <c>null</c>.
        /// Heuristic: only default/zero ids and empty lists (matches JsonUtility's expansion from null).
        /// </summary>
        private static void NormalizeBroadcastRoomStatusUnityArtifacts(object deserialized, Type requestedType) {
            if (deserialized == null || requestedType == null) return;

            if (requestedType == typeof(BroadcastRoomStatusResponse)) {
                NormalizeBroadcastRoomPayload(deserialized as BroadcastRoomStatusResponse);
                return;
            }

            if (!requestedType.IsGenericType || requestedType.GetGenericTypeDefinition() != typeof(LongEnvelope<>))
                return;

            if (requestedType.GetGenericArguments()[0] != typeof(BroadcastRoomStatusResponse))
                return;

            FieldInfo dataField = requestedType.GetField("data", BindingFlags.Instance | BindingFlags.Public);
            NormalizeBroadcastRoomPayload(dataField?.GetValue(deserialized) as BroadcastRoomStatusResponse);
        }

        private static void NormalizeBroadcastRoomPayload(BroadcastRoomStatusResponse payload) {
            if (payload?.roomInfo == null) return;
            if (!IsUnityNullExpandedRoomInfo(payload.roomInfo)) return;

            payload.roomInfo = null;
        }

        private static bool IsUnityNullExpandedRoomInfo(RoomInfo ri) {
            bool listsEmpty = (ri.basicInfos == null || ri.basicInfos.Count == 0)
                    && (ri.readyUids == null || ri.readyUids.Count == 0);
            return ri.roomId == 0 && ri.maximumPeople == 0 && listsEmpty;
        }

        private static string StripUtf8Bom(string json) {
            if (string.IsNullOrEmpty(json)) return json;
            if (json[0] == '\uFEFF')
                return json.Substring(1);
            return json;
        }

        private static FieldInfo GetInstanceField(Type declaredType, string name) {
            if (declaredType == null || string.IsNullOrEmpty(name)) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo direct = declaredType.GetField(name, flags);
            if (direct != null) return direct;
            for (Type t = declaredType.BaseType; t != null; t = t.BaseType) {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }

            return null;
        }

        /// <summary>
        /// Detects a JSON key whose value is an object with no members (only whitespace inside braces).
        /// Handles key order and spacing; does not rely on the whole payload being minified.
        /// </summary>
        private static bool JsonHasEmptyObjectForKey(string json, string key) {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;
            string needle = "\"" + key + "\"";
            int pos = 0;
            while (pos < json.Length) {
                int keyPos = json.IndexOf(needle, pos, StringComparison.Ordinal);
                if (keyPos < 0) return false;
                int i = keyPos + needle.Length;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length || json[i] != ':') {
                    pos = keyPos + 1;
                    continue;
                }

                i++;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length || json[i] != '{') {
                    pos = keyPos + 1;
                    continue;
                }

                int depth = 1;
                int contentStart = i + 1;
                i++;
                while (i < json.Length && depth > 0) {
                    char c = json[i];
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                    i++;
                }

                if (depth != 0) return false;
                int contentEnd = i - 1;
                string inner = json.Substring(contentStart, contentEnd - contentStart);
                bool onlyWhitespace = true;
                for (int j = 0; j < inner.Length; j++) {
                    if (!char.IsWhiteSpace(inner[j])) {
                        onlyWhitespace = false;
                        break;
                    }
                }

                if (!onlyWhitespace) {
                    pos = keyPos + 1;
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsEmptyJsonObject(string json) {
            if (string.IsNullOrEmpty(json)) return false;
            int i = 0;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '{') return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '}') return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return i >= json.Length;
        }

        private static bool IsMarkerStylePayloadType(Type type) {
            if (type == null || type.IsAbstract || type.IsValueType) return false;
            if (type == typeof(string)) return false;
            if (type.GetConstructor(Type.EmptyTypes) == null) return false;

            // Match JsonUtility: only public fields participate. Types with no serializable fields
            // deserialize as null when JSON is "{}"; we synthesize an instance only for that case.
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType) {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields) {
                    if (!field.IsStatic)
                        return false;
                }
            }

            return true;
        }

        private static object TryCreate(Type type) {
            try {
                return Activator.CreateInstance(type);
            } catch {
                return null;
            }
        }
    }
}