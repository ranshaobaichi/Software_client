using System.Collections.Generic;
using System.IO;

namespace Network {
    public interface IMessageFramer {
        bool TryReadMessage(Stream stream, out byte[] message);
        void WriteMessage(Stream stream, byte[] message);
    }
    
    public class LengthPrefixFramer : IMessageFramer {
        private readonly byte[] m_lengthBuffer = new byte[4];
        private byte[] m_payloadBuffer;
        private int m_payloadRead;

        public bool TryReadMessage(Stream stream, out byte[] message) {
            message = null;

            if (m_payloadBuffer == null) {
                int lenRead = 0;
                while (lenRead < 4) {
                    int n = stream.Read(m_lengthBuffer, lenRead, 4 - lenRead);
                    if (n <= 0) return false;
                    lenRead += n;
                }

                int length = (m_lengthBuffer[0] << 24) | (m_lengthBuffer[1] << 16) | (m_lengthBuffer[2] << 8) |
                             m_lengthBuffer[3];
                if (length <= 0 || length > 1024 * 1024 * 4)
                    return false;
                m_payloadBuffer = new byte[length];
                m_payloadRead = 0;
            }

            while (m_payloadRead < m_payloadBuffer.Length) {
                int n = stream.Read(m_payloadBuffer, m_payloadRead, m_payloadBuffer.Length - m_payloadRead);
                if (n <= 0) return false;
                m_payloadRead += n;
            }

            message = m_payloadBuffer;
            m_payloadBuffer = null;
            m_payloadRead = 0;
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
    
    /// <summary>
    /// Cut line framer: messages are delimited by a specific byte (default '\n').
    /// Suitable for text-based protocols, simple and human-readable,
    /// but not ideal for binary data or large messages due to potential delimiter collisions and inefficiency.
    /// </summary>
    public class LineFramer : IMessageFramer {
        private readonly byte m_delimiter;
        private readonly List<byte> m_buffer = new List<byte>();
        private readonly int m_maxLineLength;

        public LineFramer(char newLine = '\n', int maxLineLength = 0) {
            m_delimiter = (byte)newLine;
            m_maxLineLength = maxLineLength;
        }

        public bool TryReadMessage(Stream stream, out byte[] message) {
            message = null;
            int b;
            while ((b = stream.ReadByte()) >= 0) {
                m_buffer.Add((byte)b);
                if (m_maxLineLength > 0 && m_buffer.Count > m_maxLineLength) {
                    m_buffer.Clear();
                    return false;
                }

                if (b == m_delimiter) {
                    byte[] result = m_buffer.ToArray();
                    m_buffer.Clear();
                    message = result;
                    return true;
                }
            }

            // EOF reached — discard any partial line to prevent stale bytes from
            // corrupting the next message after a reconnect.
            m_buffer.Clear();
            return false;
        }

        public void WriteMessage(Stream stream, byte[] message) {
            if (message == null) return;
            stream.Write(message, 0, message.Length);
            stream.WriteByte(m_delimiter);
            stream.Flush();
        }
    }
}