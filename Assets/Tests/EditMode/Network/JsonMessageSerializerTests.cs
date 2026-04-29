using System;
using System.Text;
using Network;
using Network.Messages;
using NUnit.Framework;

namespace Tests.EditMode.Network {
    [Serializable]
    internal sealed class SerializerProbePayload {
        public int value;
    }

    [TestFixture]
    public sealed class JsonMessageSerializerTests {
        [Test]
        public void Serialize_NullPayload_ReturnsNull() {
            var serializer = new JsonMessageSerializer();

            byte[] bytes = serializer.Serialize<SerializerProbePayload>(null);

            Assert.That(bytes, Is.Null);
        }

        [Test]
        public void Deserialize_WithUtf8Bom_ParsesPayload() {
            var serializer = new JsonMessageSerializer();
            string json = "\uFEFF{\"value\":7}";

            object result = serializer.Deserialize(Encoding.UTF8.GetBytes(json), typeof(SerializerProbePayload));

            Assert.That(result, Is.TypeOf<SerializerProbePayload>());
            Assert.That(((SerializerProbePayload)result).value, Is.EqualTo(7));
        }

        [Test]
        public void Deserialize_MarkerPayloadEmptyObject_SynthesizesInstance() {
            var serializer = new JsonMessageSerializer();

            object result = serializer.Deserialize(Encoding.UTF8.GetBytes("{}"), typeof(LeaveRoomResponse));

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<LeaveRoomResponse>());
        }

        [Test]
        public void Deserialize_LongEnvelopeWithEmptyMarkerData_SetsDataInstance() {
            var serializer = new JsonMessageSerializer();
            const string json = "{\"type\":1,\"data\":{},\"pushMessages\":[3]}";

            object result = serializer.Deserialize(
                    Encoding.UTF8.GetBytes(json),
                    typeof(LongEnvelope<LeaveRoomResponse>));

            var envelope = result as LongEnvelope<LeaveRoomResponse>;
            Assert.That(envelope, Is.Not.Null);
            Assert.That(envelope.data, Is.Not.Null);
            Assert.That(envelope.pushMessages, Is.Not.Null);
            Assert.That(envelope.pushMessages.Count, Is.EqualTo(1));
            Assert.That(envelope.pushMessages[0], Is.EqualTo(3));
        }
    }
}
