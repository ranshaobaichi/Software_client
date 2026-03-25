using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UI.StateEngine;

/// <summary>
/// 对应 State.md §9 验收清单的 EditMode 单元测试。
/// 泛型 API 以类型为键，每个逻辑 State 需独立子类型（StateA/B/C/D）。
/// 通过 Reg&lt;T&gt;() 创建实例并注入引擎缓存，绕过 Unity Start() 预热阶段。
/// </summary>
[TestFixture]
public class StateEngineTests
{
    // ===== 测试辅助类 =====

    /// <summary>记录回调顺序的可复用基类。</summary>
    private class RecordState : StateBase
    {
        public List<string> Log { get; } = new();

        protected override void OnEnter()  => Log.Add("Enter");
        protected override void OnPause()  => Log.Add("Pause");
        protected override void OnResume() => Log.Add("Resume");
        protected override void OnExit()   => Log.Add("Exit");
    }

    /// <summary>独立类型，各对应一个缓存槽。</summary>
    private class StateA : RecordState { }
    private class StateB : RecordState { }
    private class StateC : RecordState { }

    /// <summary>重入测试用 State：OnResume 中尝试再压入 StateB。</summary>
    private class ReentrantState : RecordState
    {
        public StateEngine TargetEngine;
        protected override void OnResume() => TargetEngine.AddTop<StateB>();
    }

    // ===== 工厂 / 清理 =====

    private readonly List<GameObject> _created = new();

    private StateEngine NewEngine()
    {
        var go = new GameObject("StateEngine");
        _created.Add(go);
        return go.AddComponent<StateEngine>();
    }

    /// <summary>创建 T 实例并注册到引擎缓存，返回带日志能力的实例。</summary>
    private T Reg<T>(StateEngine engine, string name = "") where T : RecordState
    {
        var go = new GameObject(string.IsNullOrEmpty(name) ? typeof(T).Name : name);
        _created.Add(go);
        var state = go.AddComponent<T>();
        engine.RegisterState(state);
        return state;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _created.Clear();
    }

    // ===== §9-1：空栈 AddTop<A> → 仅 Enter、Resume =====

    [Test]
    public void AddTop_OnEmptyStack_CallsEnterThenResume()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");

        engine.AddTop<StateA>();

        Assert.AreEqual(new[] { "Enter", "Resume" }, a.Log.ToArray());
        Assert.AreEqual(1, engine.Count);
        Assert.IsTrue(ReferenceEquals(engine.Peek(), a));
    }

    // ===== §9-2：AddTop<B> → A.Pause、B.Enter、B.Resume =====

    [Test]
    public void AddTop_OnNonEmptyStack_PausesOldTopThenEntersResumesNewTop()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");

        engine.AddTop<StateA>();
        a.Log.Clear();

        engine.AddTop<StateB>();

        Assert.AreEqual(new[] { "Pause" }, a.Log.ToArray());
        Assert.AreEqual(new[] { "Enter", "Resume" }, b.Log.ToArray());
        Assert.AreEqual(2, engine.Count);
        Assert.IsTrue(ReferenceEquals(engine.Peek(), b));
    }

    // ===== §9-3：TryRemoveTop → B.Pause、B.Exit、A.Resume =====

    [Test]
    public void TryRemoveTop_CallsPauseExitOnTopThenResumeOnNewTop()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();
        a.Log.Clear();
        b.Log.Clear();

        var result = engine.TryRemoveTop();

        Assert.IsTrue(result);
        Assert.AreEqual(new[] { "Pause", "Exit" }, b.Log.ToArray());
        Assert.AreEqual(new[] { "Resume" }, a.Log.ToArray());
        Assert.AreEqual(1, engine.Count);
        Assert.IsTrue(ReferenceEquals(engine.Peek(), a));
    }

    // ===== §9-4：ReplaceTop<C> → B.Pause、B.Exit、C.Enter、C.Resume =====

    [Test]
    public void ReplaceTop_PausesExitsOldTopThenEntersResumesNewTop()
    {
        var engine = NewEngine();
        Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");
        var c = Reg<StateC>(engine, "C");

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();
        b.Log.Clear();

        engine.ReplaceTop<StateC>();

        Assert.AreEqual(new[] { "Pause", "Exit" }, b.Log.ToArray());
        Assert.AreEqual(new[] { "Enter", "Resume" }, c.Log.ToArray());
        Assert.AreEqual(2, engine.Count);
        Assert.IsTrue(ReferenceEquals(engine.Peek(), c));
    }

    // ===== §9-5：栈 [A,B,C]，TryRemoveTo<A> → C、B 依次 Pause+Exit，A.Resume =====

    [Test]
    public void TryRemoveTo_PopsAllLayersAboveTarget_InOrder()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");
        var c = Reg<StateC>(engine, "C");

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();
        engine.AddTop<StateC>();
        a.Log.Clear();
        b.Log.Clear();
        c.Log.Clear();

        var result = engine.TryRemoveTo<StateA>();

        Assert.IsTrue(result);
        Assert.AreEqual(new[] { "Pause", "Exit" }, c.Log.ToArray());
        Assert.AreEqual(new[] { "Pause", "Exit" }, b.Log.ToArray());
        Assert.AreEqual(new[] { "Resume" }, a.Log.ToArray());
        Assert.AreEqual(1, engine.Count);
        Assert.IsTrue(ReferenceEquals(engine.Peek(), a));
    }

    // ===== §9-6：重复 AddTop<A> 失败（已在栈中）=====

    [Test]
    public void AddTop_AlreadyInStack_FailsAndStackUnchanged()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");

        engine.AddTop<StateA>();
        a.Log.Clear();

        engine.AddTop<StateA>();

        Assert.AreEqual(0, a.Log.Count, "重复 AddTop 不应触发任何生命周期回调");
        Assert.AreEqual(1, engine.Count);
    }

    // ===== §9-7：Busy 时重入操作抛异常 =====

    [Test]
    public void AddTop_WhenBusy_ThrowsInvalidOperationException()
    {
        var engine = NewEngine();
        Reg<StateA>(engine, "A");
        Reg<StateB>(engine, "B");  // B 在缓存中但不在栈里，供 ReentrantState 在 OnResume 中压入

        var reentrantGo = new GameObject("Reentrant");
        _created.Add(reentrantGo);
        var reentrant = reentrantGo.AddComponent<ReentrantState>();
        reentrant.TargetEngine = engine;
        engine.RegisterState(reentrant);

        engine.AddTop<StateA>();

        // 压入 ReentrantState 时，OnResume 会尝试 AddTop<StateB>，触发重入 → 抛异常
        Assert.Throws<InvalidOperationException>(() => engine.AddTop<ReentrantState>());
    }

    // ===== 空栈 TryRemoveTop → 返回 false =====

    [Test]
    public void TryRemoveTop_OnEmptyStack_ReturnsFalse()
    {
        var engine = NewEngine();
        Assert.IsFalse(engine.TryRemoveTop());
    }

    // ===== TryRemoveTo 目标不在栈中 → false，栈不变 =====

    [Test]
    public void TryRemoveTo_TargetNotInStack_ReturnsFalseAndStackUnchanged()
    {
        var engine = NewEngine();
        Reg<StateA>(engine, "A");
        Reg<StateB>(engine, "B");
        Reg<StateC>(engine, "C");  // 注册但不入栈

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();

        var result = engine.TryRemoveTo<StateC>();

        Assert.IsFalse(result);
        Assert.AreEqual(2, engine.Count);
    }

    // ===== TryRemoveTo 目标已是栈顶 → true 无操作 =====

    [Test]
    public void TryRemoveTo_TargetAlreadyOnTop_ReturnsTrueWithNoCallbacks()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");

        engine.AddTop<StateA>();
        a.Log.Clear();

        var result = engine.TryRemoveTo<StateA>();

        Assert.IsTrue(result);
        Assert.AreEqual(0, a.Log.Count, "目标已是栈顶，不应触发任何回调");
        Assert.AreEqual(1, engine.Count);
    }

    // ===== ReplaceTop 空栈 → 退化为首次 AddTop =====

    [Test]
    public void ReplaceTop_OnEmptyStack_DegeneratesToAddTop()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");

        engine.ReplaceTop<StateA>();

        Assert.AreEqual(new[] { "Enter", "Resume" }, a.Log.ToArray());
        Assert.AreEqual(1, engine.Count);
    }

    // ===== Clear → 从顶到底依次 Pause+Exit =====

    [Test]
    public void Clear_CallsPauseExitOnAllLayersTopToBottom()
    {
        var engine = NewEngine();
        var a = Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();
        a.Log.Clear();
        b.Log.Clear();

        engine.Clear();

        Assert.AreEqual(new[] { "Pause", "Exit" }, b.Log.ToArray());
        Assert.AreEqual(new[] { "Pause", "Exit" }, a.Log.ToArray());
        Assert.IsTrue(engine.IsEmpty);
    }

    // ===== HasEntered 复位：TryRemoveTop 后再次 AddTop 重新触发 OnEnter =====

    [Test]
    public void AddTop_AfterPopAndReuse_CallsEnterAgain()
    {
        var engine = NewEngine();
        Reg<StateA>(engine, "A");
        var b = Reg<StateB>(engine, "B");

        engine.AddTop<StateA>();
        engine.AddTop<StateB>();
        engine.TryRemoveTop();  // B 弹出，HasEntered 复位
        b.Log.Clear();

        engine.AddTop<StateB>();  // 再次压入

        Assert.AreEqual(new[] { "Enter", "Resume" }, b.Log.ToArray(),
            "OnExit 后 HasEntered 应复位，再次 AddTop 必须重新触发 OnEnter");
    }

    // ===== Contains<T> 正确反映栈内状态 =====

    [Test]
    public void Contains_ReflectsStackMembership()
    {
        var engine = NewEngine();
        Reg<StateA>(engine, "A");
        Reg<StateB>(engine, "B");

        Assert.IsFalse(engine.Contains<StateA>());

        engine.AddTop<StateA>();
        Assert.IsTrue(engine.Contains<StateA>());
        Assert.IsFalse(engine.Contains<StateB>());

        engine.TryRemoveTop();
        Assert.IsFalse(engine.Contains<StateA>());
    }
}
