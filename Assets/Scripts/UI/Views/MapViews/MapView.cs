using UnityEngine;
using System.Collections.Generic;
using UI.ViewModels;
using Constants;

namespace UI.Views {
    public class MapView : ViewBase<MapViewModel> {
        [SerializeField]
        private Transform _nodeRoot;

        [SerializeField]
        private MapNodeView _nodePrefab;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private float _moveSpeed = 500f;

        [SerializeField]
        private float _maxOffset = 1000f;

        private readonly Dictionary<string, MapNodeView> m_nodeViews = new();

        public void Init(MapViewModel vm) {
            SetViewModel(vm);
        }

        protected override void Render() {
            if (ViewModel == null || ViewModel.Layers == null)
                return;

            DrawLayers(ViewModel.Layers);
        }

        private void DrawLayers(Dictionary<int, List<ServerMapNode>> layers) {
            ClearOld();

            float xSpacing = 300f;
            float ySpacing = 120f;

            foreach (var kv in layers) {
                int layerIndex = kv.Key;
                var nodes = kv.Value;

                for (int i = 0; i < nodes.Count; i++) {
                    var node = nodes[i];

                    var view = Instantiate(_nodePrefab, _nodeRoot);
                    view.Init(node);

                    view.transform.localPosition = new Vector3(
                            layerIndex * xSpacing,
                            -i * ySpacing,
                            0
                    );

                    m_nodeViews[node.nodeId] = view;
                }
            }

            foreach (var node in ViewModel.Map) {
                var from = m_nodeViews[node.nodeId];

                if (node.nextIds == null) continue;

                foreach (var nextId in node.nextIds) {
                    if (m_nodeViews.TryGetValue(nextId, out var to))
                        from.ConnectTo(to);
                }
            }
        }

        private void ClearOld() {
            foreach (var v in m_nodeViews.Values) {
                if (v != null)
                    Destroy(v.gameObject);
            }

            m_nodeViews.Clear();
        }

        private void Update() {
            HandleMouseMove();
        }

        private void HandleMouseMove() {
            float mouseX = Input.mousePosition.x;
            float screenWidth = Screen.width;

            float normalized = (mouseX / screenWidth - 0.5f) * 2f;

            float targetX = -normalized * _maxOffset;

            Vector2 pos = _content.anchoredPosition;

            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * _moveSpeed);


            pos.x = Mathf.Clamp(pos.x, GetMinX(), GetMaxX());

            _content.anchoredPosition = pos;
        }

        private float GetMinX() {
            float contentWidth = _content.rect.width;
            float viewportWidth = ((RectTransform)_content.parent).rect.width;

            float diff = contentWidth - viewportWidth;

            if (diff <= 0) return 0;

            return -diff / 2f;
        }

        private float GetMaxX() {
            float contentWidth = _content.rect.width;
            float viewportWidth = ((RectTransform)_content.parent).rect.width;

            float diff = contentWidth - viewportWidth;

            if (diff <= 0) return 0;

            return diff / 2f;
        }
    }
}