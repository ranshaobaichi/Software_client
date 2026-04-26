using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;

namespace Battle {
    /// <summary>
    /// Battle scene entry: long connection, frame queue, sync upload, entity lifecycle.
    /// </summary>
    public class BattleSession : MonoBehaviour {
        private bool m_pendingGoHome;
        #region Singleton

        private static BattleSession s_instance;
        public static BattleSession SInstance => s_instance;
        private static BattleInfo s_pendingBattleInfo;

        #endregion

        #region Inspector References

        [Header("UI")]
        [SerializeField]
        private Text _waitingStatusText;

        [SerializeField]
        private GameObject _waitingStatusPanel;

        [SerializeField]
        private Text _packetRateText;

        [SerializeField]
        private GameObject _losePanel;

        [SerializeField]
        private Button _loseSwitchHomeButton;

        [Header("Prefabs")]
        [SerializeField]
        private GameObject _playerEntityPrefab;

        [SerializeField]
        private GameObject _enemyEntityPrefab;

        [SerializeField]
        private BattleEnemyPrefabEntry[] _enemyPrefabsByType;

        [SerializeField]
        protected GameObject _bulletEntityPrefab;

        [Header("Entities")]
        [SerializeField]
        private Transform _entityRoot;

        [Header("Sync")]
        [SerializeField]
        private float m_positionSyncRateHz = 20f;

        #endregion


        #region Properties

        public INetworkChannel Channel { get; private set; }
        public int ServerFrameRate { get; private set; }
        public BattleEnemyRegistry EnemyRegistry => m_enemyRegistry;
        public BattleBulletRegistry BulletRegistry => m_bulletRegistry;
        public string MapNodeId => m_mapNodeId;
        public bool IsGameStarted => m_isGameStart;
        public GameObject BulletPrefab => _bulletEntityPrefab;

        #endregion

        #region Private Fields

        private readonly Dictionary<int, IEntity> m_playerEntities = new Dictionary<int, IEntity>();
        private readonly BattleEnemyRegistry m_enemyRegistry = new BattleEnemyRegistry();
        private readonly BattleBulletRegistry m_bulletRegistry = new BattleBulletRegistry();
        private readonly BattleFrameQueue m_frameQueue = new BattleFrameQueue();
        private readonly BattleSyncSnapshot m_syncSnapshot = new BattleSyncSnapshot();
        private readonly HashSet<int> m_framePlayerEntityIds = new HashSet<int>();

        private BattleSyncUploader m_syncUploader;
        private BattleEnemySpawner m_enemySpawner;
        private LocalPlayerCharacter m_localPlayer;
        private bool m_isGameStart;
        private bool m_channelCreated;
        private string m_mapNodeId = string.Empty;

        private int m_sendPacketCountThisSecond;
        private int m_receivePacketCountThisSecond;
        private int m_sendPacketRate;
        private int m_receivePacketRate;
        private float m_packetRateWindowStartTime;
        private string m_waitingStatusRawText = string.Empty;

        #endregion

        #region BattleInfo (F4)

        public static void SetPendingBattleInfo(BattleInfo info) {
            s_pendingBattleInfo = info;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake() {
            if (s_instance != null && s_instance != this) {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            _ApplyPendingBattleInfo();
            _InitLosePanel();
            _EnsureEnemySpawner();
            _EnsureChannel();
            _EnsureSyncUploader();
        }

        private void OnEnable() {
            _EnsureChannel();
            Channel?.Connect();
            _ResetPacketStats();

            BattleNetLog.Log($"Connect battle channel → {NetworkConstants.DefaultHost}:{NetworkConstants.BattlePort}");

            if (Channel == null || !Channel.IsConnected) {
                Debug.LogWarning("[BattleSession] Channel not ready for PlayerReadyRequest.");
                return;
            }

            Channel.Send(new PlayerReadyRequest {
                    type = (int)BattleRequestType.PLAYER_READY,
                    uid = PlayerData.SInstance.basicInfo.uid
            });
            ReportPacketSent();

            if (_waitingStatusPanel != null) {
                _waitingStatusPanel.SetActive(true);
            }
        }

        private void OnDisable() {
            BattleNetLog.Log("Disconnect battle channel (OnDisable)");
            Channel?.Disconnect();
        }

        private void OnDestroy() {
            if (s_instance == this) {
                s_instance = null;
            }

            _StopBattleLoop();
            if (Channel != null) {
                NetworkManager.SInstance.RemoveConnection(Channel);
                Channel = null;
            }

            m_channelCreated = false;
            m_syncUploader?.Dispose();
            m_syncUploader = null;

            if (_loseSwitchHomeButton != null) {
                _loseSwitchHomeButton.onClick.RemoveListener(_SwitchToHome);
            }
        }

        private void Update() {
            _UpdatePacketRate();
            _RefreshStatusUI();
            _DrainFrameQueue();
            if (m_pendingGoHome) {
                m_pendingGoHome = false;
                StartCoroutine(_HandleBattleEnd());
            }
        }

        private void LateUpdate() {
            _PublishSyncSnapshot();
        }

        #endregion

        #region Local Player Registry

        public void RegisterLocalPlayer(LocalPlayerCharacter player) {
            m_localPlayer = player;
        }

        public void UnregisterLocalPlayer(LocalPlayerCharacter player) {
            if (m_localPlayer == player) {
                m_localPlayer = null;
            }
        }

        public bool TryGetPlayerWorldPosition(string uid, out Vector2 position) {
            position = Vector2.zero;
            if (string.IsNullOrEmpty(uid)) {
                return false;
            }

            foreach (var (_, entity) in m_playerEntities) {
                if (entity is PlayerCharacter<BattlePlayerEntity> player && player.Uid == uid) {
                    var t = ((MonoBehaviour)player).transform;
                    position = t.position;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Connection Setup

        private void _EnsureEnemySpawner() {
            if (m_enemySpawner != null) {
                return;
            }

            m_enemySpawner = new BattleEnemySpawner(
                    _enemyEntityPrefab,
                    _enemyPrefabsByType,
                    _entityRoot != null ? _entityRoot : transform
            );
        }

        private void _EnsureChannel() {
            if (m_channelCreated) {
                return;
            }

            m_channelCreated = true;

            var mainHandlers = new Dictionary<BattleResponseType, LongConnectionMainDispatchEntry> {
                    {
                            BattleResponseType.BATTLE_WAIT,
                            NetworkManager.CreateDispatchEntry<BattleWaitResponse>(_OnBattleWaitResponse)
                    }, {
                            BattleResponseType.BATTLE_FRAME,
                            NetworkManager.CreateDispatchEntry<BattleFrameResponse>(_OnBattleFrameResponse)
                    }
            };

            var pushHandlers = new Dictionary<int, Action> {
                    { (int)BattlePushMessageType.BATTLE_START, _OnBattleStartPush },
                    { (int)BattlePushMessageType.BATTLE_END, _OnBattleEndPush }
            };

            Channel = NetworkManager.SInstance.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.BattlePort,
                    mainHandlers,
                    pushHandlers,
                    dispatchPushWhenMainTypeUnknown: true
            );
        }

        private void _EnsureSyncUploader() {
            if (m_syncUploader != null) {
                return;
            }

            m_syncUploader = new BattleSyncUploader(
                    m_syncSnapshot,
                    () => Channel,
                    () => PlayerData.SInstance?.basicInfo?.uid ?? string.Empty,
                    ReportPacketSent,
                    m_positionSyncRateHz
            );
        }

        #endregion

        #region Network Handlers

        private void _OnBattleWaitResponse(BattleWaitResponse response) {
            if (response == null) {
                return;
            }

            ServerFrameRate = response.gameFrame;
            m_waitingStatusRawText = $"{response.readyCount}/{response.totalCount}";
            ReportPacketReceived();
        }

        private void _OnBattleFrameResponse(BattleFrameResponse response) {
            if (!m_isGameStart || response == null) {
                return;
            }

            ReportPacketReceived();
            m_frameQueue.Enqueue(response);
        }

        private void _OnBattleStartPush() {
            if (m_isGameStart) {
                return;
            }

            ReportPacketReceived();
            m_isGameStart = true;

            if (_waitingStatusPanel != null) {
                _waitingStatusPanel.SetActive(false);
            }

            m_syncUploader?.SetSendRateHz(m_positionSyncRateHz);
            m_syncUploader?.Start();
        }

        private void _OnBattleEndPush() {
            ReportPacketReceived();

            m_isGameStart = false;
            m_pendingGoHome = true;
        }

        #endregion

        #region Frame Queue

        private void _DrainFrameQueue() {
            m_frameQueue.DrainAll(_ApplyBattleFrame);
        }

        private void _ApplyBattleFrame(BattleFrameResponse frame) {
            if (frame == null) {
                return;
            }

            if (frame.events != null) {
                foreach (var evt in frame.events) {
                    _ApplyEvent(evt);
                }
            }

            _BootstrapEnemiesFromFrame(frame.enemyEntities);
            _SyncPlayerEntities(frame.playerEntities);
        }

        private void _BootstrapEnemiesFromFrame(List<BattleEnemyEntity> enemyEntities) {
            if (enemyEntities == null || enemyEntities.Count == 0) {
                return;
            }

            _EnsureEnemySpawner();
            foreach (var enemyData in enemyEntities) {
                if (enemyData == null || enemyData.entityId == 0) {
                    continue;
                }

                if (m_enemyRegistry.TryGet(enemyData.entityId, out _)) {
                    continue;
                }

                m_enemySpawner.Spawn(enemyData, m_enemyRegistry);
            }
        }

        #endregion

        #region Events

        private void _ApplyEvent(BattleEventDTO evt) {
            if (evt == null) {
                return;
            }

            switch (evt.eventType) {
                case BattleEventType.ENEMY_SPAWN:
                    _SpawnEnemyFromEvent(evt.spawnParameter);
                    break;
                case BattleEventType.BULLET_SPAWN:
                    _SpawnBulletFromEvent(evt.spawnParameter);
                    break;
                case BattleEventType.ENEMY_INTENT_CHANGE:
                    m_enemyRegistry.ApplyIntentChange(evt.intentParameter);
                    break;
                case BattleEventType.ENTITY_DAMAGE:
                    _ApplyDamage(evt.damageParameter);
                    break;
                case BattleEventType.ENTITY_DESTROY:
                    _ApplyDestroy(evt.destroyParameter);
                    break;
                case BattleEventType.BULLET_HIT_ENEMY:
                case BattleEventType.BULLET_HIT_PLAYER:
                case BattleEventType.BULLET_HIT_WALL:
                    _ApplyBulletHit(evt.hitParameter);
                    break;
            }
        }

        private void _SpawnEnemyFromEvent(BattleEventSpawnParameter spawn) {
            _EnsureEnemySpawner();
            m_enemySpawner.SpawnFromEvent(spawn, m_enemyRegistry);
        }

        private void _SpawnBulletFromEvent(BattleEventSpawnParameter spawn) {
            var bulletData = spawn?.bulletEntity;

            if (bulletData == null || _bulletEntityPrefab == null)
                return;

            // 防重复（服务器可能重复下发）
            if (m_bulletRegistry.TryGet(bulletData.entityId, out _))
                return;

            var go = Instantiate(_bulletEntityPrefab, _entityRoot);

            var bullet =
                    go.GetComponent<BulletEntity>()
                    ?? go.AddComponent<BulletEntity>();

            bullet.InitIfNot(bulletData);
        }

        private void _ApplyDamage(BattleEventDamageParameter damage) {
            if (damage == null) {
                return;
            }

            if (damage.targetEntityType == BattleEntityType.ENEMY &&
                m_enemyRegistry.TryGet(damage.targetEntityId, out var enemy)) {
                enemy.ApplyDamage(damage.damage, damage.currentHP);
            }
        }

        private void _ApplyDestroy(BattleEventDestroyParameter destroy) {
            if (destroy == null) {
                return;
            }

            var id = destroy.entityId;
            if (m_playerEntities.TryGetValue(id, out var player)) {
                player.RemoveSelf();
                m_playerEntities.Remove(id);
                return;
            }

            if (m_enemyRegistry.TryGet(id, out var enemy)) {
                enemy.RemoveSelf();
                return;
            }

            if (m_bulletRegistry.TryGet(id, out var bullet)) {
                bullet.RemoveSelf();
            }
        }

        private void _ApplyBulletHit(BattleEventHitParameter hit) {
            if (hit == null) {
                return;
            }

            if (m_bulletRegistry.TryGet(hit.sourceEntityId, out var bullet)) {
                bullet.RemoveSelf();
            }
        }

        #endregion

        #region Sync Snapshot

        private void _PublishSyncSnapshot() {
            if (!m_isGameStart) {
                return;
            }

            m_localPlayer?.PublishSyncSnapshot(m_syncSnapshot);
            m_syncSnapshot.SetEnemyPositions(m_enemyRegistry.GetAllReportPositions());
        }

        #endregion

        #region Packet Stats

        public void ReportPacketSent() {
            m_sendPacketCountThisSecond++;
        }

        public void ReportPacketReceived() {
            m_receivePacketCountThisSecond++;
        }

        private void _ResetPacketStats() {
            m_sendPacketCountThisSecond = 0;
            m_receivePacketCountThisSecond = 0;
            m_sendPacketRate = 0;
            m_receivePacketRate = 0;
            m_packetRateWindowStartTime = Time.time;
            m_waitingStatusRawText = string.Empty;
        }

        private void _UpdatePacketRate() {
            if (m_packetRateWindowStartTime <= 0f) {
                m_packetRateWindowStartTime = Time.time;
                return;
            }

            if (Time.time - m_packetRateWindowStartTime < 1f) {
                return;
            }

            m_sendPacketRate = m_sendPacketCountThisSecond;
            m_receivePacketRate = m_receivePacketCountThisSecond;
            m_sendPacketCountThisSecond = 0;
            m_receivePacketCountThisSecond = 0;
            m_packetRateWindowStartTime = Time.time;
        }

        private void _RefreshStatusUI() {
            string packetRateText = $"TX:{m_sendPacketRate}/s RX:{m_receivePacketRate}/s";
            if (_packetRateText != null) {
                _packetRateText.text = packetRateText;
            } else if (_waitingStatusText != null) {
                _waitingStatusText.text = string.IsNullOrEmpty(m_waitingStatusRawText)
                        ? packetRateText
                        : $"{m_waitingStatusRawText}\n{packetRateText}";
            }
        }

        #endregion

        #region Player Entities

        private void _SyncPlayerEntities(List<BattlePlayerEntity> playerEntities) {
            if (playerEntities == null || playerEntities.Count == 0) {
                _DestroyAllPlayers();
                return;
            }

            var localUid = PlayerData.SInstance?.basicInfo?.uid;
            m_framePlayerEntityIds.Clear();

            foreach (var entity in playerEntities) {
                if (entity == null) {
                    continue;
                }

                m_framePlayerEntityIds.Add(entity.entityId);
                bool isLocal = !string.IsNullOrEmpty(localUid) && entity.uid == localUid;

                if (m_playerEntities.TryGetValue(entity.entityId, out var existing)) {
                    if (!isLocal) {
                        existing.ReceiveData(entity);
                    } else if (existing is LocalPlayerCharacter local) {
                        local.OnReceiveData(entity);
                    }
                } else {
                    _SpawnPlayerEntity(entity, localUid);
                }
            }

            var toRemove = new List<int>();
            foreach (var (entityId, _) in m_playerEntities) {
                if (!m_framePlayerEntityIds.Contains(entityId)) {
                    toRemove.Add(entityId);
                }
            }

            foreach (var id in toRemove) {
                if (m_playerEntities.TryGetValue(id, out var entity) && entity != null) {
                    entity.RemoveSelf();
                }

                m_playerEntities.Remove(id);
            }
        }

        private void _SpawnPlayerEntity(BattlePlayerEntity entityData, string localUid) {
            bool isLocal = !string.IsNullOrEmpty(localUid) && entityData.uid == localUid;
            var go = Instantiate(_playerEntityPrefab);
            IEntity entityInterface;

            if (isLocal) {
                entityInterface = go.GetComponent<LocalPlayerCharacter>() ?? go.AddComponent<LocalPlayerCharacter>();
            } else {
                entityInterface = go.GetComponent<RemotePlayerCharacter>() ?? go.AddComponent<RemotePlayerCharacter>();
            }

            if (entityInterface == null) {
                Destroy(go);
                Debug.LogError("[BattleSession] Failed to add player entity component.");
                return;
            }

            entityInterface.InitIfNot(entityData);
            m_playerEntities[entityInterface.EntityId] = entityInterface;
        }

        private void _DestroyAllPlayers() {
            foreach (var (_, entity) in m_playerEntities) {
                entity?.RemoveSelf();
            }

            m_playerEntities.Clear();
            m_localPlayer = null;
        }

        #endregion

        #region Battle Lifecycle

        private void _StopBattleLoop() {
            m_isGameStart = false;
            m_syncUploader?.Stop();
            m_frameQueue.Clear();
            _DestroyAllActors();
            Channel?.Disconnect();
        }

        private void _DestroyAllActors() {
            _DestroyAllPlayers();
            m_enemyRegistry.Clear();
            m_bulletRegistry.Clear();
        }

        private void _ApplyPendingBattleInfo() {
            if (s_pendingBattleInfo == null) {
                return;
            }

            m_mapNodeId = s_pendingBattleInfo.mapNodeId ?? string.Empty;
            s_pendingBattleInfo = null;
        }

        private void _InitLosePanel() {
            if (_losePanel != null) {
                _losePanel.SetActive(false);
            }

            if (_loseSwitchHomeButton != null) {
                _loseSwitchHomeButton.onClick.AddListener(_SwitchToHome);
            }
        }

        private void _ShowLosePanel() {
            if (_losePanel != null) {
                _losePanel.SetActive(true);
            }
        }

        private void _SwitchToHome() {
            GameSceneManager.SInstance.SwitchScene(SceneType.HOME);
        }
        private System.Collections.IEnumerator _GoHome() {
            yield return new WaitForSeconds(0.1f);
            _SwitchToHome();
        }
        private IEnumerator _HandleBattleEnd(){
            _ShowLosePanel();

            // 等一帧：让 frame queue / event 彻底结束
            yield return null;

            // 停同步（先停上报）
            m_isGameStart = false;
            m_syncUploader?.Stop();

            // 清 frame
            m_frameQueue.Clear();

            yield return null;

            // ⚠️ 关键修复：不要在 foreach 中 clear
            _SafeDestroyAllActors();

            yield return null;

            if (Channel != null) {
                Channel.Disconnect();
            }

            yield return null;

            GameSceneManager.SInstance.SwitchScene(SceneType.HOME);
        }
        private void _SafeDestroyAllActors() {
            _DestroyAllPlayers();
            m_enemyRegistry.Clear();
            m_bulletRegistry.Clear();
        }
        #endregion
    }
}