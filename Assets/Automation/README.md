# Automation

Headless **action-level** session API for driving the real client network stack against the C++ server (no UI). Designed for the test harness **Orchestrator → Unity Worker** model: each Worker process holds one `uid` via `PlayerData`; the Orchestrator (in `game-test-harness`) sends IPC commands that map to individual IEnumerator methods below.

## Architecture role

| Layer | Location | Responsibility |
|-------|----------|----------------|
| **Automation** | `Assets/Automation/` (this folder) | Single-action coroutines per session; no process orchestration |
| **Orchestrator** | `game-test-harness` | Spawn/maintain Workers, coordinate multi-player sync, IPC |
| **Unity Worker** | One batchmode Unity per uid | `AutomationBootstrap.Init` → execute one action → report result |

**Hard constraint**: do not modify `Assets/Scripts/` (including `PlayerData`, `NetworkManager`). One Unity process = one uid.

See also: [客户端Automation方案.plan.md](../../.cursor/plans/客户端Automation方案.plan.md), [测试仓库方案.plan.md](../../.cursor/plans/测试仓库方案.plan.md).

## Ports (default)

| Port | Service |
|------|---------|
| 8765 | Login (register / login / logout) |
| 8766 | Lobby (rooms, ready, broadcast, GET_STATE_STATUS) |
| 8767 | Shop |
| 8768 | Map |
| 8769 | Battle |

Configure via `NetworkEndpointConfig` (`BattlePort` is not in game `NetworkConstants`).

## Phase order

```
LOBBY → SHOP → MAP → BATTLE → END
```

| Transition | Trigger |
|------------|---------|
| LOBBY → SHOP | All members `SET_READY`; push `pushMessages: [0]` |
| SHOP → MAP | Any player sends `MAP_INIT` (not a shop opcode) |
| MAP → BATTLE | All players `MAP_MOVE` same `selectId`; push `pushMessages: [1]` |
| BATTLE → END | push `pushMessages: [1]` (BATTLE_END) |

## Action API (IPC surface)

All methods are `IEnumerator`; Worker invokes one at a time.

### AuthSession

| Method | Notes |
|--------|-------|
| `RegisterAndLogin(onUid?)` | Register + Login; initializes `PlayerData` |
| `Register(onUid?)` | `{type:1}` only |
| `Login(uid)` | Requires uid |
| `Logout()` | Clears `PlayerData`; **call after LeaveRoom** |

`GameClientSession.LogoutSafely()` = LeaveRoom → disconnect domain sessions → Logout.

### LobbySession (8766)

| Method | Notes |
|--------|-------|
| `EnsureLongConnection()` | Open Home long connection + push dispatch |
| `ListRooms(onRooms?)` | Short request LIST_ROOMS |
| `CreateRoom(maximumPeople)` | CREATE_ROOM + long connection |
| `JoinRoom(roomId)` | JOIN_ROOM + long connection |
| `SetReady(ready)` | No direct response; read BROADCAST |
| `WaitForAllReady()` | Waits push `0` → room enters SHOP |
| `LeaveRoom()` | LEAVE_ROOM on long connection |
| `GetStateStatus(onStatus?)` | **GET_STATE_STATUS (type=9)**; see below |

### ShopSession (8767)

| Method | Notes |
|--------|-------|
| `Skip()` | **No-op.** SHOP→MAP is triggered by `Map.InitMap`, not shop commands |
| `Init()` | SHOP_INIT + wait SHOP_SYNC |
| `AutoBuyFirst()` | Init + buy first affordable item |

### MapSession (8768)

| Method | Notes |
|--------|-------|
| `InitMap(roomId)` | MAP_INIT; also advances SHOP→MAP |
| `SelectDefaultRoute()` | First root via `MapRouteHelper` |
| `SelectRoute(selectId)` | MAP_MOVE (no direct response) |
| `WaitForMapCommit()` | Waits push `1` (all same selectId) |

### BattleSession (8769)

| Method | Notes |
|--------|-------|
| `Connect()` | Battle long connection |
| `PlayerReady()` | PLAYER_READY |
| `WaitForBattleStart()` | Waits push `0` |
| `SendPositionSync(x, y, dirX, dirY)` | Single POSITION_SYNC (type=1); for stepwise Worker drive |
| `SendShoot(dirX, dirY)` | PLAYER_SHOOT |
| `IdleUntilEnd(timeout?)` | Loops POSITION_SYNC until BATTLE_END |

## GET_STATE_STATUS (type=9)

Short request on Home port (8766). Optional poll for Orchestrator/GUI; push-based waits still work without it.

Request: `{"type": 9, "uid": "<uid>"}`

Response fields (`GetStateStatusResponse`):

| Field | Meaning |
|-------|---------|
| `online` | Player online |
| `roomId` | -1 if not in room |
| `roomPhase` | 0=LOBBY, 1=SHOP, 2=MAP, 3=BATTLE, 4=END |
| `roomMemberCount` | Members in room |
| `allLobbyReady` | All lobby ready |
| `mapNodeId` | Committed map node, -1 if none |
| `battleTick` | Battle tick (0 outside battle) |

## End-to-end example (single Worker)

```csharp
using Automation.Bootstrap;
using Automation.Sessions;
using Automation.Config;

AutomationBootstrap.Init(new NetworkEndpointConfig { Host = "127.0.0.1" });

var client = new GameClientSession();
yield return client.RunSinglePlayerFlow(maximumPeople: 4);
```

Step-by-step (Orchestrator maps each line to a separate IPC action):

```csharp
yield return client.Auth.RegisterAndLogin();
yield return client.Lobby.CreateRoom(4);
yield return client.Lobby.SetReady(true);
yield return client.Lobby.WaitForAllReady();
yield return client.Shop.Skip();
yield return client.Map.InitMap(client.Lobby.RoomId);
yield return client.Map.SelectDefaultRoute();
yield return client.Map.WaitForMapCommit();
yield return client.Battle.Connect();
yield return client.Battle.PlayerReady();
yield return client.Battle.WaitForBattleStart();
yield return client.Battle.SendPositionSync(); // optional single tick
yield return client.Battle.IdleUntilEnd();
yield return client.LogoutSafely();
```

Poll state during orchestration:

```csharp
GetStateStatusResponse status = null;
yield return client.Lobby.GetStateStatus(s => status = s);
// status.roomPhase == (int)RoomPhase.SHOP after ALL_READY
```

## Rules

- Always **LEAVE_ROOM before LOGOUT** (use `LogoutSafely()`).
- Multi-instance: one uid per Unity Worker process (`PlayerData` singleton).
- Multi-player sync: same `selectId` on MAP_MOVE; all `PLAYER_READY`.
- Use server `battle_config.test.json` with low `durationSeconds` for automated battle.

## Test server (WSL)

```bash
./build/server --config config/server.json --duration-seconds 5
```
