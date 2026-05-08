using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Network;
using Network.Messages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Network {
    [Serializable]
    internal sealed class DispatcherPayloadA : ServerNetworkMessages {
        public int value;
    }

    internal enum DispatcherMainOpcode {
        PayloadA = 1
    }

    [TestFixture]
    public sealed class LongConnectionInboundDispatcherTests {
        private const int PushMarker = 99;

        [Test]
        public void Dispatch_MainTypeAndPush_InvokesBothHandlers() {
            object received = null;
            int pushCalls = 0;
            var main = new Dictionary<int, LongConnectionMainDispatchEntry> {
                    {
                            (int)DispatcherMainOpcode.PayloadA,
                            new LongConnectionMainDispatchEntry(typeof(DispatcherPayloadA), o => received = o)
                    },
            };
            var push = new Dictionary<int, Action> {
                    { PushMarker, () => pushCalls++ },
            };
            var tables = new LongConnectionDispatchTables(main, push, dispatchPushWhenMainTypeUnknown: true);
            var serializer = new JsonMessageSerializer();
            byte[] raw = Encoding.UTF8.GetBytes("{\"type\":1,\"data\":{\"value\":42},\"pushMessages\":[99]}");

            LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);

            Assert.That(received, Is.TypeOf<DispatcherPayloadA>());
            Assert.That(((DispatcherPayloadA)received).value, Is.EqualTo(42));
            Assert.That(pushCalls, Is.EqualTo(1));
        }

        [Test]
        public void Dispatch_UnknownMainType_DispatchesPush_WhenFlagTrue() {
            int pushCalls = 0;
            var tables = new LongConnectionDispatchTables(
                    new Dictionary<int, LongConnectionMainDispatchEntry>(),
                    new Dictionary<int, Action> { { PushMarker, () => pushCalls++ } },
                    dispatchPushWhenMainTypeUnknown: true);
            var serializer = new JsonMessageSerializer();
            byte[] raw = Encoding.UTF8.GetBytes("{\"type\":404,\"data\":{},\"pushMessages\":[99]}");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[LongConnectionInboundDispatcher\] Unknown main type 404"));
            LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);

            Assert.That(pushCalls, Is.EqualTo(1));
        }

        [Test]
        public void Dispatch_UnknownMainType_SkipsPush_WhenFlagFalse() {
            int pushCalls = 0;
            var tables = new LongConnectionDispatchTables(
                    new Dictionary<int, LongConnectionMainDispatchEntry>(),
                    new Dictionary<int, Action> { { PushMarker, () => pushCalls++ } },
                    dispatchPushWhenMainTypeUnknown: false);
            var serializer = new JsonMessageSerializer();
            byte[] raw = Encoding.UTF8.GetBytes("{\"type\":404,\"data\":{},\"pushMessages\":[99]}");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[LongConnectionInboundDispatcher\] Unknown main type 404"));
            LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);

            Assert.That(pushCalls, Is.EqualTo(0));
        }
    }
}