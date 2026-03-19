using UnityEngine;
using System.Collections.Generic;
using Network;
using Network.Messages;

public class SimpleNetworkClient2D : MonoBehaviour {
    [Header("Server")]
    public string host = Constants.NetworkConstants.DefaultHost;
    public int port = Constants.NetworkConstants.DefaultPort;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float sendInterval = 0.05f;
    public float remoteLerp = 15f;

    private INetworkChannel _channel;
    private string _myId = "";
    private float _sendTimer = 0f;

    private readonly Dictionary<string, GameObject> _remoteObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Vector3> _remoteTargets = new Dictionary<string, Vector3>();

    private void Start() {
        _channel = NetworkManager.SInstance.CreateConnection(host, port);
        _channel.RegisterHandler<GameMessage>(OnMessage);
        _channel.Connect();
        
#if UNITY_EDITOR
        Application.runInBackground = true;
#endif
    }

    private void Update() {
        HandleLocalMovement();
        UpdateRemoteVisuals();
    }

    private void OnDestroy() {
        if (_channel != null) {
            NetworkManager.SInstance.RemoveConnection(_channel);
            _channel = null;
        }
    }

    private void OnMessage(GameMessage msg) {
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        if (msg.type == "welcome") {
            _myId = msg.id ?? "";
            Debug.Log("[Net] My id: " + _myId);
            return;
        }

        if (msg.type != "snapshot" || msg.players == null) return;

        HashSet<string> alive = new HashSet<string>();
        foreach (NetPlayer p in msg.players) {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            alive.Add(p.id);

            Vector3 targetPos = new Vector3(p.x, p.y, 0f);
            if (p.id == _myId) continue;

            if (!_remoteObjects.ContainsKey(p.id))
                _remoteObjects[p.id] = CreateRemoteObject("Remote_" + p.id, targetPos);
            _remoteTargets[p.id] = targetPos;
        }

        List<string> toRemove = new List<string>();
        foreach (var kv in _remoteObjects) {
            if (!alive.Contains(kv.Key)) toRemove.Add(kv.Key);
        }

        foreach (string id in toRemove) {
            Destroy(_remoteObjects[id]);
            _remoteObjects.Remove(id);
            _remoteTargets.Remove(id);
        }
    }

    private void HandleLocalMovement() {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 delta = new Vector3(h, v, 0f).normalized * moveSpeed * Time.deltaTime;
        transform.position += delta;

        _sendTimer += Time.deltaTime;
        if (_channel != null && _channel.IsConnected && _sendTimer >= sendInterval) {
            _sendTimer = 0f;
            _channel.Send(new MoveMsg { x = transform.position.x, y = transform.position.y });
        }
    }

    private void UpdateRemoteVisuals() {
        foreach (var kv in _remoteObjects) {
            string id = kv.Key;
            GameObject go = kv.Value;
            if (!_remoteTargets.ContainsKey(id)) continue;

            go.transform.position = Vector3.Lerp(
                    go.transform.position,
                    _remoteTargets[id],
                    remoteLerp * Time.deltaTime
            );
        }
    }

    private GameObject CreateRemoteObject(string objectName, Vector3 pos) {
        GameObject go = new GameObject(objectName);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite(new Color(1f, 0.45f, 0.1f, 1f));
        go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        return go;
    }

    private Sprite CreateSolidSprite(Color color) {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        Rect rect = new Rect(0, 0, 1, 1);
        return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 1f);
    }
}