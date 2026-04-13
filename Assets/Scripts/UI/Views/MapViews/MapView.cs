using System;
using UnityEngine;
using System.Collections.Generic;
using Constants;
using UI.ViewModels;
using Network.Messages;
using UI.StateEngine;

namespace UI.Views {
    public class MapView : ViewBase<MapViewModel> {
        [SerializeField] private Transform _nodeRoot;
        [SerializeField] private MapNodeView _nodePrefab;
        [SerializeField] private RectTransform _content;

        [SerializeField] private float _moveSpeed = 500f;
        [SerializeField] private float _maxOffset = 1000f;

        [SerializeField] private RectTransform _linePrefab;
        [SerializeField] private RectTransform _lineRoot;

        private bool m_dragging;
        private Vector2 m_lastMousePos;

        private readonly Dictionary<int, MapNodeView> m_nodeViews = new();
        private Action<int> m_onClick;
        public void Init(MapViewModel vm, Action<int> onClick) {
            SetViewModel(vm);
            m_onClick = onClick;
        }

        protected override void Render() {
            if (ViewModel == null)
                return;
            
            if (ViewModel.Layers == null || ViewModel.Map == null)
                return;

            DrawLayers(ViewModel.Layers);
        }

        public void OnNodeClicked(int nodeId) {
            Debug.Log($"Enter battle from node: {nodeId}");
            m_onClick(nodeId);
            GameSceneManager.SInstance.SwitchScene(SceneType.BATTLE);
            //UIStateFinder stateFinder;
            //stateFinder.Current(this).AddTop<ShopState>();
        }

        public void RefreshNodeSelectState() {
            if (ViewModel == null) return;

            string localUid = PlayerData.SInstance.basicInfo.uid;

            foreach (var kv in m_nodeViews) {
                var nodeId = kv.Key;
                var view = kv.Value;

                string owner = ViewModel.GetOwner(nodeId);

                if (owner == null) {
                    view.ClearSelect();
                } else if (owner == localUid) {
                    view.SetSelfSelected();
                } else {
                    view.SetOtherSelected();
                }
            }
        }

        private void DrawLayers(Dictionary<int, List<MapNode>> layers) {
            ClearOld();

            float xSpacing = 300f;
            float ySpacing = 120f;

            // 1. 创建节点
            foreach (var kv in layers) {
                int layerIndex = kv.Key;
                var nodes = kv.Value;

                for (int i = 0; i < nodes.Count; i++) {
                    var node = nodes[i];

                    var view = Instantiate(_nodePrefab, _nodeRoot);
                    view.Init(node, OnNodeClicked);

                    view.transform.localPosition = new Vector3(
                        layerIndex * xSpacing,
                        -i * ySpacing,
                        0
                    );

                    m_nodeViews[node.nodeId] = view;
                }
            }

            // 2. 等节点全部生成后再画线（避免 null）
            DrawConnections(ViewModel.Map);
        }

        private void DrawConnections(List<MapNode> map) {
            if (map == null) return;

            foreach (var node in map) {
                if (!m_nodeViews.TryGetValue(node.nodeId, out var from))
                    continue;

                if (node.nextId == null)
                    continue;

                foreach (var nextId in node.nextId) {
                    if (m_nodeViews.TryGetValue(nextId, out var to)) {
                        DrawUILine(
                            from.GetComponent<RectTransform>(),
                            to.GetComponent<RectTransform>()
                        );
                    }
                }
            }
        }

        private void ClearOld() {
            foreach (var v in m_nodeViews.Values) {
                if (v != null)
                    Destroy(v.gameObject);
            }

            m_nodeViews.Clear();
            for (int i = _lineRoot.childCount - 1; i >= 0; i--) {
                Destroy(_lineRoot.GetChild(i).gameObject);
            }
        }

        private void Update() {
            HandleDrag();
        }

        private void HandleDrag() {
            if (Input.GetMouseButtonDown(0)) {
                m_dragging = true;
                m_lastMousePos = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0)) {
                m_dragging = false;
            }

            if (!m_dragging) return;

            Vector2 current = Input.mousePosition;
            Vector2 delta = current - m_lastMousePos;
            m_lastMousePos = current;

            _content.anchoredPosition += new Vector2(delta.x, 0);
        }

        private void DrawUILine(RectTransform from, RectTransform to) {
            if (from == null || to == null) return;

            var line = Instantiate(_linePrefab, _lineRoot);

            Vector3 fromPos = from.position;
            Vector3 toPos = to.position;

            Vector3 dir = toPos - fromPos;
            float dist = dir.magnitude;

            line.position = fromPos;
            line.sizeDelta = new Vector2(dist, 6f);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            line.rotation = Quaternion.Euler(0, 0, angle);

            line.SetAsFirstSibling();
        }
    }
}