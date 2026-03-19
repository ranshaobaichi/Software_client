# Copilot Instructions for Unity Repository

This repository is a Unity (C#) project with strict requirements on code style, runtime performance, and engineering conventions.

You MUST follow these instructions when generating, modifying, or reviewing code.

---

# 1. High-Level Overview

- Project type: Unity game / application
- Language: C#
- Runtime: Unity (Mono / IL2CPP)
- Key concern areas:
    - Runtime performance (especially per-frame execution)
    - GC allocation control
    - Unity lifecycle correctness
    - Code maintainability and consistency

This is NOT a general-purpose C# project. Unity-specific constraints apply everywhere.

---

# 2. Source of Truth: .editorconfig (MANDATORY)

The `.editorconfig` file in this repository is the **single source of truth** for code style.

## Rules

- ALWAYS follow `.editorconfig` when generating or modifying code
- NEVER introduce formatting inconsistent with `.editorconfig`
- If existing code violates `.editorconfig`, DO NOT copy the violation
- Explicitly fix style issues when touching code

## Typical enforced areas include:

- Naming conventions
- Brace style (`{}` placement)
- Spacing / indentation
- Access modifiers
- Member ordering
- var vs explicit type

Violating `.editorconfig` is considered a **review issue (P1 or P2 depending on severity)**.

---

# 3. Unity-Specific Engineering Rules (CRITICAL)

## 3.1 Lifecycle Correctness

You MUST respect Unity lifecycle semantics:

- `Awake` → initialization independent of other objects
- `OnEnable` → subscribe to events
- `Start` → initialization that depends on other objects
- `OnDisable` / `OnDestroy` → unsubscribe / cleanup

### REQUIRED

- ALWAYS unsubscribe from events
- NEVER leave dangling delegates
- AVOID order-dependent initialization unless explicitly handled

---

## 3.2 Update / Frame Loop Constraints

`Update`, `LateUpdate`, `FixedUpdate`, `OnGUI` are HOT PATHS.

### NEVER do:

- Heavy computation
- LINQ
- Allocations (`new`, `ToList`, `string concat`, etc.)
- `GetComponent` repeatedly
- `Find`, `GameObject.Find`, `Camera.main`

### ALWAYS:

- Cache references
- Use preallocated structures
- Move logic out of Update when possible

Violations are at least **P1 (often P0 if severe)**.

---

## 3.3 LINQ Usage Policy

- LINQ is **DISCOURAGED in all runtime code**
- Treat LINQ in gameplay/runtime code as a **problem by default**
- Allowed only if:
    - clearly not in hot path
    - no allocation risk
    - strongly justified

Otherwise: **P1 or P2 issue**

---

## 3.4 Component & API Usage

Avoid expensive Unity APIs in runtime loops:

- `GetComponent` → cache result
- `FindObjectOfType` → avoid or cache
- `Resources.Load` → avoid in runtime paths
- `Instantiate/Destroy` → avoid frequent usage, prefer pooling

---

## 3.5 Serialization Rules

- Only serializable fields should be `[SerializeField]`
- Avoid runtime mutation of ScriptableObject shared data
- Be careful with:
    - reference sharing
    - unintended asset mutation
    - hidden coupling

---

# 4. Code Structure & Member Ordering

Unless otherwise specified by the repository, follow this order:

1. `const` / `static readonly`
2. `[SerializeField]` fields
3. private fields
4. public properties
5. Unity lifecycle methods
6. public methods
7. private methods

Incorrect ordering is a **P2 issue**.

---

# 5. Performance & GC Constraints

Unity performance is highly sensitive to allocations.

## 5.1 Avoid GC Allocations

Common sources to avoid:

- LINQ
- lambda / closures
- iterator (`yield`)
- boxing
- string concatenation in loops
- temporary collections
- foreach (in some cases)

## 5.2 Hot Path Awareness

Always ask:

> Is this executed every frame or frequently?

If YES:
- ZERO allocation preferred
- minimal branching
- no reflection
- no dynamic lookup

---

## 5.3 Object Lifecycle

Check for:

- event leaks
- static references preventing GC
- improper pooling
- missing cleanup

---

# 6. Code Quality Requirements

You MUST ensure:

- No null reference risks
- Clear ownership of data
- No duplicated logic
- No dead code
- Clear and consistent naming
- Methods have single responsibility

Avoid:

- God classes
- Deep nesting
- Hidden side effects

---

# 7. CI / Validation Expectations

Before considering a change correct:

- Code compiles in Unity
- No obvious runtime errors
- No violation of `.editorconfig`
- No Unity lifecycle misuse
- No performance regressions in hot paths

If uncertain, prefer safer implementation.

---

# 8. Review Severity Model

When reviewing or generating suggestions, classify issues:

- **P0**: Critical (crash, broken runtime, corrupted state)
- **P1**: Must fix (serious bug, performance issue, rule violation)
- **P2**: Should fix (maintainability / moderate issue)
- **P3**: Suggestion (better pattern / improvement)

---

# 9. How Copilot Should Behave

- Do NOT assume this is standard C# — always consider Unity context
- Do NOT introduce LINQ casually
- Do NOT introduce allocations in Update-like methods
- ALWAYS follow `.editorconfig`
- Prefer explicit, safe, and performant code over concise code
- Prefer readability over cleverness
- If unsure, choose conservative and predictable implementation

---

# 10. When to Search the Codebase

Only search the repository if:

- conventions are unclear
- lifecycle usage differs from standard
- architecture requires context

Otherwise, TRUST these instructions.

---

# 11. Summary

This repository prioritizes:

1. Correctness (no runtime bugs)
2. Performance (no unnecessary allocations)
3. Consistency (strict `.editorconfig`)
4. Unity best practices

Violating any of the above is likely to cause PR rejection.