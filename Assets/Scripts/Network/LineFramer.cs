using System.Collections.Generic;
using System.IO;

namespace Network {
    /// <summary>
    /// Cut line framer: messages are delimited by a specific byte (default '\n').
    /// Suitable for text-based protocols, simple and human-readable,
    /// but not ideal for binary data or large messages due to potential delimiter collisions and inefficiency.
    /// </summary>
    public class LineFramer : IMessageFramer {
        private readonly byte _delimiter;
        private readonly List<byte> _buffer = new List<byte>();
        private readonly int _maxLineLength;

        /// <param name="newLine">Line end marker character</param>
        public LineFramer(char newLine = '\n', int maxLineLength = 0) {
            _delimiter = (byte)newLine;
            _maxLineLength = maxLineLength;
        }

        public bool TryReadMessage(Stream stream, out byte[] message) {
            message = null;
            int b;
            while ((b = stream.ReadByte()) >= 0) {
                _buffer.Add((byte)b);
                if (_maxLineLength > 0 && _buffer.Count > _maxLineLength) {
                    _buffer.Clear();
                    return false;
                }

                if (b == _delimiter) {
                    byte[] result = _buffer.ToArray();
                    _buffer.Clear();
                    message = result;
                    return true;
                }
            }

            return false;
        }

        public void WriteMessage(Stream stream, byte[] message) {
            if (message == null) return;
            stream.Write(message, 0, message.Length);
            stream.WriteByte(_delimiter);
            stream.Flush();
        }
    }
}