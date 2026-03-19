using System.IO;

namespace Network {
    public interface IMessageFramer {
        bool TryReadMessage(Stream stream, out byte[] message);
        void WriteMessage(Stream stream, byte[] message);
    }
}