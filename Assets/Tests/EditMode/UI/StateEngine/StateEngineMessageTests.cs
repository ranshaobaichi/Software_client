using UnityEngine;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UI.StateEngine;

[TestFixture]
public class StateEngineMessageTests {
    private GameObject m_host;
    private StateEngine m_engine;

    [SetUp]
    public void SetUp() {
        m_host = new GameObject("StateEngineMessageTestHost");
        m_engine = m_host.AddComponent<StateEngine>();
    }

    [TearDown]
    public void TearDown() {
        if (m_host != null)
            UnityEngine.Object.DestroyImmediate(m_host);
    }

    private class ReceiverState : StateBase {
        public Dictionary<Type, object> LastReceivedMessages;

        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                LastReceivedMessages = null;
                return;
            }

            LastReceivedMessages = new Dictionary<Type, object>(messages);
        }
    }

    private class StateA : ReceiverState { }

    private class StateB : ReceiverState { }

    private class StateC : ReceiverState { }

    private T CreateStateComponent<T>(string name = null) where T : StateBase {
        var go = new GameObject(name ?? typeof(T).Name);
        var comp = go.AddComponent<T>();
        go.transform.SetParent(m_host.transform, false);
        comp.gameObject.SetActive(false);
        return comp;
    }

    private T CreateAndRegisterState<T>(string name = null) where T : StateBase {
        var comp = CreateStateComponent<T>(name);
        m_engine.RegisterState(comp);
        return comp;
    }

    [Test]
    public void SendMessage_SendBeforeBecomeTop_MessageConsumed() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();

        // Act
        m_engine.SendMessage<StateA, StateB>("message");
        m_engine.AddTop<StateB>();
        
        // Arrange
        Assert.IsNotNull(b.LastReceivedMessages);
        Assert.IsTrue(b.LastReceivedMessages.TryGetValue(typeof(StateA), out var got));
        Assert.AreEqual("message", got);
    }

    [Test]
    public void SendMessage_SameMessage_CoverOldMessage() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();

        // Act
        m_engine.SendMessage<StateA, StateB>("first");
        m_engine.SendMessage<StateA, StateB>("second");
        m_engine.AddTop<StateB>();

        // Assert
        Assert.IsNotNull(b.LastReceivedMessages);
        Assert.IsTrue(b.LastReceivedMessages.TryGetValue(typeof(StateA), out var got));
        Assert.AreEqual("second", got);
    }

    [Test]
    public void SendMessage_MultipleSenders_AllMessagesDelivered() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var c = CreateAndRegisterState<StateC>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateC>();

        // Act
        m_engine.SendMessage<StateA, StateB>("A_to_B");
        m_engine.SendMessage<StateC, StateB>("C_to_B");
        m_engine.AddTop<StateB>();

        // Assert
        Assert.IsNotNull(b.LastReceivedMessages);
        Assert.AreEqual(2, b.LastReceivedMessages.Count);
        Assert.AreEqual("A_to_B", b.LastReceivedMessages[typeof(StateA)]);
        Assert.AreEqual("C_to_B", b.LastReceivedMessages[typeof(StateC)]);
    }
    
    [Test]
    public void ReceiveMessage_SendAfterBecomeTop_MessageNotConsumed() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();

        // Act
        m_engine.SendMessage<StateA, StateB>("message");

        // Assert
        Assert.IsNull(b.LastReceivedMessages);

        // Act
        var c = CreateAndRegisterState<StateC>();
        m_engine.AddTop<StateC>();
        m_engine.TryRemoveTop();
        
        // Assert: check if message is delivered and not consumed until now
        Assert.IsNotNull(b.LastReceivedMessages);
        Assert.IsTrue(b.LastReceivedMessages.TryGetValue(typeof(StateA), out var got));
        Assert.AreEqual("message", got);
    }

    [Test]
    public void ReceiveMessage_TryRemoveTop_MessageConsumed() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        
        // Act
        m_engine.SendMessage<StateB, StateA>("message");
        m_engine.TryRemoveTop();
        
        // Assert
        Assert.IsNotNull(a.LastReceivedMessages);   
        Assert.IsTrue(a.LastReceivedMessages.TryGetValue(typeof(StateB), out var got));
        Assert.AreEqual("message", got);
    }
    
    [Test]
    public void ReceiveMessage_ReplaceTop_MessageConsumed() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        var c = CreateAndRegisterState<StateC>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        
        // Act
        m_engine.SendMessage<StateA, StateC>("message");
        m_engine.ReplaceTop<StateC>();
        
        // Assert
        Assert.IsNotNull(c.LastReceivedMessages);   
        Assert.IsTrue(c.LastReceivedMessages.TryGetValue(typeof(StateA), out var got));
        Assert.AreEqual("message", got);
    }
    
    [Test]
    public void ReceiveMessage_TryRemoveTo_MessageConsumed() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        
        // Act
        m_engine.SendMessage<StateB, StateA>("message");
        m_engine.TryRemoveTo<StateA>();
        
        // Assert
        Assert.IsNotNull(a.LastReceivedMessages);   
        Assert.IsTrue(a.LastReceivedMessages.TryGetValue(typeof(StateB), out var got));
        Assert.AreEqual("message", got);
    }
    
    [Test]
    public void ReceiveMessage_RemovedByTryRemoveToBeforeReceive_MessageDiscarded() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        var c = CreateAndRegisterState<StateC>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        m_engine.AddTop<StateC>();
        
        // Act
        m_engine.SendMessage<StateC, StateB>("message");
        m_engine.TryRemoveTo<StateA>();
        m_engine.AddTop<StateB>();
        
        // Assert
        Assert.IsNull(b.LastReceivedMessages);
    }

    [Test]
    public void ReceiveMessage_ClearedBeforeReceive_MessageDiscarded() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        
        // Act
        m_engine.SendMessage<StateB, StateA>("message");
        m_engine.Clear();
        m_engine.AddTop<StateB>();
        
        // Assert
        Assert.IsNull(a.LastReceivedMessages);
    }
}