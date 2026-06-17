## 对比目标：`api_docs`（api_docs_has_structs）

- 仓库：`client`（比对 **client** 方向文档块与代码）
- 对比目标：`api_docs`
- 文件数：4
- 文档消息块：4
- 代码消息类型：30
- scope 内类型数：37

## 结论

- **缺陷**：文档消息 `武器::client::` 在代码中未找到对应类型
- **缺陷**：文档消息 `准备 (切换场景完成)::client::` 在代码中未找到对应类型
- **缺陷**：文档消息 `位置同步::client::` 在代码中未找到对应类型
- **缺陷**：文档消息 `攻击::client::` 在代码中未找到对应类型
- **缺陷**：代码类型 `SimpleNetworkClient2D` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ClientNetworkMessage` 在文档（同方向）中未描述
- **缺陷**：代码类型 `NetworkErrorMessage` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShortEnvelope` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LongEnvelope` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LongConnectionProbeEnvelope` 在文档（同方向）中未描述
- **缺陷**：代码类型 `RoomInfo` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShopItem` 在文档（同方向）中未描述
- **缺陷**：代码类型 `playerInfos` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LoginRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LoginResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `RegisterResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LogoutRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `CreateRoomRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `CreateRoomResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `JoinRoomRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `JoinRoomResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `RefreshRoomResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `SetReadyStatusRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `BroadcastRoomStatusResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `LeaveRoomRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShopInitRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShopMoveCursorRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShopBuyRequest` 在文档（同方向）中未描述
- **缺陷**：代码类型 `ShopSyncResponse` 在文档（同方向）中未描述
- **缺陷**：代码类型 `NetPlayer` 在文档（同方向）中未描述
- … 另有 4 条

## 消息级对比

| 章节 | 方向 | 消息 | 状态 | 匹配 | 缺代码字段 | 缺文档字段 | 类型不一致 |
|------|------|------|------|------|------------|------------|------------|
| 武器 | client | 武器::client:: | missing_in_code |  | 0 | 0 | 0 |
| 准备 (切换场景完成) | client | 准备 (切换场景完成)::client:: | missing_in_code |  | 0 | 0 | 0 |
| 位置同步 | client | 位置同步::client:: | missing_in_code |  | 0 | 0 | 0 |
| 攻击 | client | 攻击::client:: | missing_in_code |  | 0 | 0 | 0 |
| SimpleNetworkClient2D | client | SimpleNetworkClient2D | missing_in_doc |  | 0 | 0 | 0 |
| ClientNetworkMessage | client | ClientNetworkMessage | missing_in_doc |  | 0 | 0 | 0 |
| NetworkErrorMessage | client | NetworkErrorMessage | missing_in_doc |  | 0 | 0 | 0 |
| ShortEnvelope | client | ShortEnvelope | missing_in_doc |  | 0 | 0 | 0 |
| LongEnvelope | client | LongEnvelope | missing_in_doc |  | 0 | 0 | 0 |
| LongConnectionProbeEnvelope | client | LongConnectionProbeEnvelope | missing_in_doc |  | 0 | 0 | 0 |
| RoomInfo | client | RoomInfo | missing_in_doc |  | 0 | 0 | 0 |
| ShopItem | client | ShopItem | missing_in_doc |  | 0 | 0 | 0 |
| playerInfos | client | playerInfos | missing_in_doc |  | 0 | 0 | 0 |
| LoginRequest | client | LoginRequest | missing_in_doc |  | 0 | 0 | 0 |
| LoginResponse | client | LoginResponse | missing_in_doc |  | 0 | 0 | 0 |
| RegisterResponse | client | RegisterResponse | missing_in_doc |  | 0 | 0 | 0 |
| LogoutRequest | client | LogoutRequest | missing_in_doc |  | 0 | 0 | 0 |
| CreateRoomRequest | client | CreateRoomRequest | missing_in_doc |  | 0 | 0 | 0 |
| CreateRoomResponse | client | CreateRoomResponse | missing_in_doc |  | 0 | 0 | 0 |
| JoinRoomRequest | client | JoinRoomRequest | missing_in_doc |  | 0 | 0 | 0 |
| JoinRoomResponse | client | JoinRoomResponse | missing_in_doc |  | 0 | 0 | 0 |
| RefreshRoomResponse | client | RefreshRoomResponse | missing_in_doc |  | 0 | 0 | 0 |
| SetReadyStatusRequest | client | SetReadyStatusRequest | missing_in_doc |  | 0 | 0 | 0 |
| BroadcastRoomStatusResponse | client | BroadcastRoomStatusResponse | missing_in_doc |  | 0 | 0 | 0 |
| LeaveRoomRequest | client | LeaveRoomRequest | missing_in_doc |  | 0 | 0 | 0 |
| ShopInitRequest | client | ShopInitRequest | missing_in_doc |  | 0 | 0 | 0 |
| ShopMoveCursorRequest | client | ShopMoveCursorRequest | missing_in_doc |  | 0 | 0 | 0 |
| ShopBuyRequest | client | ShopBuyRequest | missing_in_doc |  | 0 | 0 | 0 |
| ShopSyncResponse | client | ShopSyncResponse | missing_in_doc |  | 0 | 0 | 0 |
| NetPlayer | client | NetPlayer | missing_in_doc |  | 0 | 0 | 0 |
| WelcomeMsg | client | WelcomeMsg | missing_in_doc |  | 0 | 0 | 0 |
| SnapshotMsg | client | SnapshotMsg | missing_in_doc |  | 0 | 0 | 0 |
| MoveMsg | client | MoveMsg | missing_in_doc |  | 0 | 0 | 0 |
| GameMessage | client | GameMessage | missing_in_doc |  | 0 | 0 | 0 |

## 扫描文件

- `Assets/Scripts/Constants/NetworkConstants.cs`
- `Assets/Scripts/Network/NetModels/ServerNetworkMessages.cs`
- `Assets/Scripts/SimpleNetworkClient2D.cs`
- `config/message_aliases.yaml`