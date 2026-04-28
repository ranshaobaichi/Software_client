# Software Client

A Unity game client built with C#, featuring a stack-based UI state engine, MVVM bindings, a modal dialog system, and a TCP network layer.

---

## Table of Contents

- [Project Structure](#project-structure)
- [Architecture Overview](#architecture-overview)
  - [Scene & Page Layer](#scene--page-layer)
  - [State Engine](#state-engine)
  - [MVVM Layer](#mvvm-layer)
  - [Dialog System](#dialog-system)
  - [Network Layer](#network-layer)
- [Scenes](#scenes)
- [Documentation](#documentation)
- [Code Conventions](#code-conventions)

---

## Project Structure

```
Assets/
├── Scenes/               # Unity scenes (Login, Home, Battle, Sample)
├── Scripts/
│   ├── Constants/        # Enums shared across systems (SceneType, NetworkConstants)
│   ├── Network/          # TCP network manager, channel, framer, serializer
│   ├── Singletons/       # Cross-scene singletons (GameSceneManager)
│   ├── UI/
│   │   ├── Dialogs/      # Dialog instances grouped by page
│   │   ├── Models/       # Shared data models
│   │   ├── Pages/        # Page MonoBehaviours (BattlePage, CommonPage)
│   │   ├── StateEngine/  # Core state stack engine and page/dialog subsystems
│   │   ├── States/       # StateBase subclasses (Login, Home, Battle states)
│   │   ├── ViewModels/   # ViewModelBase subclasses
│   │   ├── Views/        # ViewBase subclasses
│   │   └── MVVMFactory.cs
│   └── Battle/           # Battle-specific singletons
├── Tests/
│   └── EditMode/         # Edit-mode unit tests
.docs/                    # Design documents (State, Page, MiniDialog)
```

---

## Architecture Overview

### Scene & Page Layer

`GameSceneManager` is a cross-scene singleton (`DontDestroyOnLoad`) that handles transitions between the three top-level scenes with optional fade animation:

| Scene | SceneType |
|-------|-----------|
| `LoginScene` | `LOGIN` |
| `HomeScene` | `HOME` |
| `BattleScene` | `BATTLE` |

Within a scene, `PageController` manages **Pages** — top-level UI containers each backed by an independent `StateEngine`. Only one page is active at a time. `HomePage` is treated specially: instead of being destroyed on navigation away, it is deactivated and reused on return.

```
PageController
  └── StateEnginePage  (active page)
        ├── Canvas / GraphicRaycaster / CanvasScaler
        └── StateEngine
              ├── State A
              └── State B
```

See [`.docs/Page.md`](.docs/Page.md) for the full page lifecycle specification.

### State Engine

`StateEngine` maintains a **LIFO stack** of `StateBase` instances. Each state goes through four lifecycle hooks:

| Hook | When called |
|------|-------------|
| `OnEnter` | Every time the state is pushed onto the stack |
| `OnPause` | Just before another state is pushed on top |
| `OnResume` | When the state becomes the top again |
| `OnExit` | After the state is removed from the stack |

States can pass data to each other using `SendMessage<TFrom, TTo>(message)`, which is consumed once via `ReceiveMessage` when the target state next becomes the top.

Available stack operations via `IStateEngine`:

```csharp
engine.AddTop<T>();           // push a new state
engine.TryRemoveTop();        // pop the top state
engine.ReplaceTop<T>();       // swap the top state
engine.TryRemoveTo<T>();      // pop down to a specific state
engine.Clear();               // pop everything
engine.SendMessage<TFrom, TTo>(message);
```

See [`.docs/State.md`](.docs/State.md) for the full specification.

### MVVM Layer

Views derive from `ViewBase<TVm>` and are bound to `ViewModelBase<TVm>` subclasses via `MVVMFactory.Bind(view, vm)`. On bind, the view immediately renders the current state and subscribes to `PropertyChanged` for subsequent updates. Subscriptions are automatically cleaned up in `OnDestroy`.

```csharp
MVVMFactory.Bind<MyView, MyViewModel>(gameObject, new MyViewModel());
```

### Dialog System

`DialogMgr` manages a stack of modal dialogs within a state. Dialogs are instantiated on open and destroyed on close — there is no pre-warming.

```csharp
// In a State's OnEnter:
_dialogMgr = DialogMgr.Create(new DialogMgr.Builder { ... });

// Open a dialog:
int id = _dialogMgr.OpenDialog<MyDialog, MyInput>(DialogType.MyDialog, inputData, callbackImpl);

// Close it:
_dialogMgr.CloseDialog(id, result);

// In OnExit — required to prevent leaks:
_dialogMgr.Dispose();
```

Dialog prefabs must have a `DialogBase` (or `DialogBase<TInput>`) component at the root. All dialog types are registered in a `DialogRegistry` ScriptableObject.

See [`.docs/MiniDialog.md`](.docs/MiniDialog.md) for the full specification.

### Network Layer

`NetworkManager` is a thread-safe singleton that manages one or more `INetworkChannel` connections. Network callbacks are marshalled back to the **Unity main thread** via a queue that is drained in `NetworkManager.Update()`.

Key classes:

| Class | Role |
|-------|------|
| `NetworkManager` | Singleton; registers channels, pumps messages per frame |
| `NetworkChannel` (TCP) | Enqueues raw bytes; `DispatchPendingMessages()` decodes and dispatches |
| `MessageFramer` | Length-prefix framing |
| `MessageSerializer` | Serializes / deserializes message payloads |
| `LongConnectionInboundDispatcher` | Routes inbound messages to registered handlers |

---

## Scenes

| Scene | Purpose |
|-------|---------|
| `LoginScene` | Authentication flow |
| `HomeScene` | Lobby, online room, and shop |
| `BattleScene` | In-game map and battle |
| `SampleScene` | Development sandbox |

---

## Documentation

Detailed design specifications live in the [`.docs/`](.docs/) directory:

- [`.docs/State.md`](.docs/State.md) — State / StateEngine lifecycle and API
- [`.docs/Page.md`](.docs/Page.md) — Page / PageController lifecycle and API
- [`.docs/MiniDialog.md`](.docs/MiniDialog.md) — Dialog system design

---

## Code Conventions

This project follows the rules defined in [`.editorconfig`](.editorconfig):

| Symbol type | Naming convention |
|-------------|------------------|
| Private / protected instance fields | `m_` prefix (e.g. `m_health`) |
| `[SerializeField]` private / protected fields | `_` prefix (e.g. `_speed`) |
| Private / protected static fields | `s_` prefix (e.g. `s_instance`) |
| Public members | PascalCase, no prefix |

Member order within a class: `const` / `static readonly` → `[SerializeField]` fields → private fields → properties → Unity lifecycle methods → public methods → private methods.
