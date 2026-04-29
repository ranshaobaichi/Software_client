using System.IO;
using System.Text;
using Network;
using NUnit.Framework;

namespace Tests.EditMode.Network {
    [TestFixture]
    public sealed class MessageFramerTests {
        [Test]
        public void LengthPrefixFramer_WriteThenRead_RoundTripsPayload() {
            var framer = new LengthPrefixFramer();
            byte[] payload = Encoding.UTF8.GetBytes("hello-network");
            using var stream = new MemoryStream();

            framer.WriteMessage(stream, payload);
            stream.Position = 0;

            bool ok = framer.TryReadMessage(stream, out byte[] readBack);

            Assert.That(ok, Is.True);
            Assert.That(readBack, Is.EqualTo(payload));
        }

        [Test]
        public void LengthPrefixFramer_TryReadMessage_WithZeroLength_ReturnsFalse() {
            var framer = new LengthPrefixFramer();
            using var stream = new MemoryStream(new byte[] { 0, 0, 0, 0 });

            bool ok = framer.TryReadMessage(stream, out byte[] message);

            Assert.That(ok, Is.False);
            Assert.That(message, Is.Null);
        }

        [Test]
        public void LineFramer_TryReadMessage_ReadsDelimitedMessage() {
            var framer = new LineFramer('\n');
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ping\n"));

            bool ok = framer.TryReadMessage(stream, out byte[] message);

            Assert.That(ok, Is.True);
            Assert.That(Encoding.UTF8.GetString(message), Is.EqualTo("ping\n"));
        }

        [Test]
        public void LineFramer_TryReadMessage_EofClearsPartialBuffer() {
            var framer = new LineFramer('\n');
            using var first = new MemoryStream(Encoding.UTF8.GetBytes("stale-partial"));
            bool firstRead = framer.TryReadMessage(first, out _);
            Assert.That(firstRead, Is.False);

            using var second = new MemoryStream(Encoding.UTF8.GetBytes("fresh\n"));
            bool secondRead = framer.TryReadMessage(second, out byte[] message);

            Assert.That(secondRead, Is.True);
            Assert.That(Encoding.UTF8.GetString(message), Is.EqualTo("fresh\n"));
        }
    }
}
