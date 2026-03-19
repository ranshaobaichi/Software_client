using System;

namespace Network {
    public interface IMessageSerializer {
        byte[] Serialize<T>(T payload) where T : class;
        object Deserialize(byte[] data, Type type);
    }
}