// using System;
// using System.Collections.Generic;
// using Network;
// using Network.Messages;
// using NUnit.Framework;
//
// namespace Tests.EditMode.Network {
//     internal enum TestMainOpcode {
//         PayloadA = 1,
//         PayloadB = 2,
//     }
//
//     [Serializable]
//     internal sealed class TestPayloadA : ServerNetworkSuccessMessage {
//         public int value;
//     }
//
//     [Serializable]
//     internal sealed class TestPayloadB : ServerNetworkSuccessMessage {
//         public string text;
//     }
//
//     public sealed class LongConnectionInboundDispatcherTests {
//         private const int PushMarker = 99;
//
//         [Test]
//         public void Dispatch_MainTypeAndPush_InvokesHandlers() {
//             object receivedData = null;
//             int pushCalls = 0;
//             var main = new Dictionary<int, LongConnectionMainDispatchEntry> {
//                     {
//                             (int)TestMainOpcode.PayloadA,
//                             new LongConnectionMainDispatchEntry(typeof(TestPayloadA), o => receivedData = o)
//                     },
//             };
//             var push = new Dictionary<int, Action> {
//                     { PushMarker, () => pushCalls++ },
//             };
//             var tables = new LongConnectionDispatchTables(main, push, dispatchPushWhenMainTypeUnknown: true);
//             var serializer = new JsonMessageSerializer();
//
//             const string json =
//                     "{\"type\":1,\"data\":{\"value\":42},\"pushMessages\":[99]}";
//             byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
//
//             LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);
//
//             Assert.IsInstanceOf<TestPayloadA>(receivedData);
//             Assert.AreEqual(42, ((TestPayloadA)receivedData).value);
//             Assert.AreEqual(1, pushCalls);
//         }
//
//         [Test]
//         public void Dispatch_UnknownMainType_StillDispatchesPush_WhenFlagTrue() {
//             int pushCalls = 0;
//             var main = new Dictionary<int, LongConnectionMainDispatchEntry>();
//             var push = new Dictionary<int, Action> {
//                     { PushMarker, () => pushCalls++ },
//             };
//             var tables = new LongConnectionDispatchTables(main, push, dispatchPushWhenMainTypeUnknown: true);
//             var serializer = new JsonMessageSerializer();
//             byte[] raw = System.Text.Encoding.UTF8.GetBytes(
//                     "{\"type\":999,\"data\":{},\"pushMessages\":[99]}");
//
//             LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);
//
//             Assert.AreEqual(1, pushCalls);
//         }
//
//         [Test]
//         public void Dispatch_UnknownMainType_SkipsPush_WhenFlagFalse() {
//             int pushCalls = 0;
//             var main = new Dictionary<int, LongConnectionMainDispatchEntry>();
//             var push = new Dictionary<int, Action> {
//                     { PushMarker, () => pushCalls++ },
//             };
//             var tables = new LongConnectionDispatchTables(main, push, dispatchPushWhenMainTypeUnknown: false);
//             var serializer = new JsonMessageSerializer();
//             byte[] raw = System.Text.Encoding.UTF8.GetBytes(
//                     "{\"type\":999,\"data\":{},\"pushMessages\":[99]}");
//
//             LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);
//
//             Assert.AreEqual(0, pushCalls);
//         }
//
//         [Test]
//         public void Dispatch_SecondDeserialize_SelectsCorrectDataType() {
//             object received = null;
//             var main = new Dictionary<int, LongConnectionMainDispatchEntry> {
//                     {
//                             (int)TestMainOpcode.PayloadB,
//                             new LongConnectionMainDispatchEntry(typeof(TestPayloadB), o => received = o)
//                     },
//             };
//             var push = new Dictionary<int, Action>();
//             var tables = new LongConnectionDispatchTables(main, push, true);
//             var serializer = new JsonMessageSerializer();
//             byte[] raw = System.Text.Encoding.UTF8.GetBytes(
//                     "{\"type\":2,\"data\":{\"text\":\"hi\"},\"pushMessages\":null}");
//
//             LongConnectionInboundDispatcher.Dispatch(raw, serializer, tables);
//
//             Assert.IsInstanceOf<TestPayloadB>(received);
//             Assert.AreEqual("hi", ((TestPayloadB)received).text);
//         }
//     }
// }
