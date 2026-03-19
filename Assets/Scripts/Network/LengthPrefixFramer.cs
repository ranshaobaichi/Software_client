using System.IO;

namespace Network {
    public class LengthPrefixFramer : IMessageFramer {
        private readonly byte[] _lengthBuffer = new byte[4];
        private byte[] _payloadBuffer;
        private int _payloadRead;

        public bool TryReadMessage(Stream stream, out byte[] message) {
            message = null;

            if (_payloadBuffer == null) {
                int lenRead = 0;
                while (lenRead < 4) {
                    int n = stream.Read(_lengthBuffer, lenRead, 4 - lenRead);
                    if (n <= 0) return false;
                    lenRead += n;
                }

                int length = (_lengthBuffer[0] << 24) | (_lengthBuffer[1] << 16) | (_lengthBuffer[2] << 8) |
                             _lengthBuffer[3];
                if (length <= 0 || length > 1024 * 1024 * 4)
                    return false;
                _payloadBuffer = new byte[length];
                _payloadRead = 0;
            }

            while (_payloadRead < _payloadBuffer.Length) {
                int n = stream.Read(_payloadBuffer, _payloadRead, _payloadBuffer.Length - _payloadRead);
                if (n <= 0) return false;
                _payloadRead += n;
            }

            message = _payloadBuffer;
            _payloadBuffer = null;
            _payloadRead = 0;
            return true;
        }

        public void WriteMessage(Stream stream, byte[] message) {
            if (message == null) return;
            int len = message.Length;
            stream.WriteByte((byte)(len >> 24));
            stream.WriteByte((byte)(len >> 16));
            stream.WriteByte((byte)(len >> 8));
            stream.WriteByte((byte)len);
            stream.Write(message, 0, len);
            stream.Flush();
        }
    }
}