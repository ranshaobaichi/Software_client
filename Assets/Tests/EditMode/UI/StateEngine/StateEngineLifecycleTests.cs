using UnityEngine;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UI.StateEngine;
using UnityEngine.TestTools;

[TestFixture]
public class StateEngineLifecycleTests {
    private GameObject m_host;
    private StateEngine m_engine;

    [SetUp]
    public void SetUp() {
        m_host = new GameObject("StateEngineTestHost");
        m_engine = m_host.AddComponent<StateEngine>();
    }

    [TearDown]
    public void TearDown() {
        if (m_host != null)
            UnityEngine.Object.DestroyImmediate(m_host);
    }

    private class RecordState : StateBase {
        private readonly List<string> m_log = new();
        public IReadOnlyList<string> Log => m_log;

        protected override void OnEnter() => m_log.Add("Enter");
        protected override void OnPause() => m_log.Add("Pause");
        protected override void OnResume() => m_log.Add("Resume");
        protected override void OnExit() => m_log.Add("Exit");

        public void ClearLog() => m_log.Clear();
    }

    private class StateA : RecordState { }

    private class StateB : RecordState { }

    private class StateC : RecordState { }

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
    public void Peek_OnEmptyStack_ReturnNull() {
        // Arrange

        // Act

        // Assert
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
    }

    [Test]
    public void Peek_WhenNotEmpty_ReturnTop() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        bool ret = m_engine.AddTop<StateA>();

        // Act

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
    }

    [Test]
    public void Contains_WhenRegistered_ReturnTrue() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        bool ret = m_engine.AddTop<StateA>();

        // Act

        // Assert
        Assert.IsTrue(ret);
        Assert.IsTrue(m_engine.Contains<StateA>());
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
    }

    [Test]
    public void Contains_WhenNotRegistered_ReturnFalse() {
        // Arrange

        // Act

        // Assert
        Assert.IsFalse(m_engine.Contains<StateA>());
    }

    [Test]
    public void Clear_WhenNotEmpty_ExitAllStatesAndClearMessages() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        bool retA = m_engine.AddTop<StateA>();
        bool retB = m_engine.AddTop<StateB>();
        a.ClearLog();
        b.ClearLog();

        // Act
        m_engine.Clear();

        // Assert
        Assert.IsTrue(retA && retB);
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
        Assert.IsFalse(a.gameObject.activeSelf);
        Assert.IsFalse(b.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Exit" }, a.Log);
        CollectionAssert.AreEqual(new[] { "Pause", "Exit" }, b.Log);
    }

    [Test]
    public void Clear_OnEmptyStack_DoNothing() {
        // Arrange

        // Act
        m_engine.Clear();

        // Assert
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
    }

    [Test]
    public void AddTop_OnEmptyStack_EnterAndResume() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();

        // Act
        bool ret = m_engine.AddTop<StateA>();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Enter", "Resume" }, a.Log);
    }

    [Test]
    public void AddTop_WhenNotEmpty_PausePrevious() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateB>();
        b.ClearLog();

        // Act
        bool ret = m_engine.AddTop<StateA>();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(2, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        Assert.IsFalse(b.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Pause" }, b.Log);
        CollectionAssert.AreEqual(new[] { "Enter", "Resume" }, a.Log);
    }

    [Test]
    public void AddTop_DuplicatedType_DoNothing() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var other_a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();

        // Act
        bool ret = m_engine.AddTop<StateA>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        Assert.IsFalse(other_a.gameObject.activeSelf);
        CollectionAssert.AreEqual(Array.Empty<string>(), a.Log);
    }

    [Test]
    public void AddTop_UnregisteredType_LogError() {
        // Arrange
        LogAssert.Expect(LogType.Error,
                $"[StateEngine] 类型 {nameof(StateA)} 未在缓存中找到，请先通过 Inspector 注册或调用 RegisterState()。");

        // Act
        bool ret = m_engine.AddTop<StateA>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void AddTop_NullType_ReturnFalseAndDoNothing() {
        // Arrange

        // Act
        bool ret = m_engine.AddTop(null);

        // Assert
        Assert.IsFalse(ret);
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
    }

    [Test]
    public void TryRemoveTop_NotEmptyAfterPop_PopAndResumePrevious() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        a.ClearLog();
        b.ClearLog();

        // Act
        bool ret = m_engine.TryRemoveTop();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        Assert.IsFalse(b.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Pause", "Exit" }, b.Log);
        CollectionAssert.AreEqual(new[] { "Resume" }, a.Log);
    }

    [Test]
    public void TryRemoveTop_EmptyAfterPop_PopAndResumePrevious() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();

        // Act
        bool ret = m_engine.TryRemoveTop();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
        Assert.IsFalse(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Pause", "Exit" }, a.Log);
    }

    [Test]
    public void TryRemoveTop_OnEmptyStack_ReturnFalseAndDoNothing() {
        // Arrange

        // Act
        bool ret = m_engine.TryRemoveTop();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsTrue(m_engine.isEmpty);
        Assert.AreEqual(0, m_engine.count);
        Assert.IsNull(m_engine.Peek());
    }

    [Test]
    public void ReplaceTop_WhenNotEmpty_ReplaceTopState() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateB>();
        b.ClearLog();

        // Act
        bool ret = m_engine.ReplaceTop<StateA>();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        Assert.IsFalse(b.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Pause", "Exit" }, b.Log);
        CollectionAssert.AreEqual(new[] { "Enter", "Resume" }, a.Log);
    }

    [Test]
    public void ReplaceTop_OnEmptyStack_ReplaceTopState() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();

        // Act
        bool ret = m_engine.ReplaceTop<StateA>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Enter", "Resume" }, a.Log);
    }

    [Test]
    public void ReplaceTop_ContainsNewTop_LogError() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();
        LogAssert.Expect(LogType.Error,
                $"[StateEngine] ReplaceTop 失败：{nameof(StateA)} 已在栈中。");

        // Act
        bool ret = m_engine.ReplaceTop<StateA>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(Array.Empty<string>(), a.Log);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ReplaceTop_UnregisteredType_DoNothingAndReturn() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();
        LogAssert.Expect(LogType.Error,
                $"[StateEngine] 类型 {nameof(StateB)} 未在缓存中找到，请先通过 Inspector 注册或调用 RegisterState()。");

        // Act
        bool ret = m_engine.ReplaceTop<StateB>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(Array.Empty<string>(), a.Log);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void TryRemoveTo_TargetIsNotPeek_PopUntilTargetAndResume() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        var b = CreateAndRegisterState<StateB>();
        m_engine.AddTop<StateA>();
        m_engine.AddTop<StateB>();
        a.ClearLog();
        b.ClearLog();

        // Act
        bool ret = m_engine.TryRemoveTo<StateA>();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        Assert.IsFalse(b.gameObject.activeSelf);
        CollectionAssert.AreEqual(new[] { "Pause", "Exit" }, b.Log);
        CollectionAssert.AreEqual(new[] { "Resume" }, a.Log);
    }

    [Test]
    public void TryRemoveTo_TargetIsPeek_ReturnTrueAndDoNothing() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();

        // Act
        bool ret = m_engine.TryRemoveTo<StateA>();

        // Assert
        Assert.IsTrue(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(Array.Empty<string>(), a.Log);
    }

    [Test]
    public void TryRemoveTo_CanNotFindTarget_ReturnFalseAndDoNothing() {
        // Arrange
        var a = CreateAndRegisterState<StateA>();
        m_engine.AddTop<StateA>();
        a.ClearLog();

        // Act
        bool ret = m_engine.TryRemoveTo<StateB>();

        // Assert
        Assert.IsFalse(ret);
        Assert.IsFalse(m_engine.isEmpty);
        Assert.AreEqual(1, m_engine.count);
        Assert.IsTrue(ReferenceEquals(a, m_engine.Peek()));
        Assert.IsTrue(a.gameObject.activeSelf);
        CollectionAssert.AreEqual(Array.Empty<string>(), a.Log);
    }
}