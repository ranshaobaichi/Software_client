using UnityEngine;
using System;
using System.Text;

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

        public byte[] Serialize<T>(T payload) where T : class {
            if (payload == null) return null;
            string json = JsonUtility.ToJson(payload);
            return json == null ? null : Utf8.GetBytes(json);
        }

        public object Deserialize(byte[] data, Type type) {
            if (data == null || data.Length == 0 || type == null) return null;
            string json = Utf8.GetString(data).Trim();
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson(json, type);
        }
    }
}