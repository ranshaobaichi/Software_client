using UnityEngine;
using System.Collections.Generic;
using UI.ViewModels;
using Constants;

namespace UI.Views
{
    public class MapView : ViewBase<MapViewModel>
    {
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

        [SerializeField]
        private RectTransform _linePrefab;

        [SerializeField]
        private RectTransform _lineRoot;
        
        
        private bool m_dragging;
        private Vector2 m_lastMousePos;
        private Vector2 m_targetPos;
        private readonly Dictionary<string, MapNodeView> m_nodeViews = new();

        public void Init(MapViewModel vm)
        {
            SetViewModel(vm);
        }

        protected override void Render()
        {
            if (ViewModel == null || ViewModel.Layers == null)
                return;

            DrawLayers(ViewModel.Layers);
        }

        private void DrawLayers(Dictionary<int, List<ServerMapNode>> layers)
        {
            ClearOld();

            float xSpacing = 300f;
            float ySpacing = 120f;

            foreach (var kv in layers)
            {
                int layerIndex = kv.Key;
                var nodes = kv.Value;

                for (int i = 0; i < nodes.Count; i++)
                {
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

            // 连线
            foreach (var node in ViewModel.Map)
            {
                var from = m_nodeViews[node.nodeId];

                if (node.nextIds == null)
                    continue;

                foreach (var nextId in node.nextIds)
                {
                    if (m_nodeViews.TryGetValue(nextId, out var to))
                    {
                        DrawUILine(
                            from.GetComponent<RectTransform>(),
                            to.GetComponent<RectTransform>()
                        );
                    }
                }
            }
        }

        private void ClearOld()
        {
            foreach (var v in m_nodeViews.Values)
            {
                if (v != null)
                    Destroy(v.gameObject);
            }

            m_nodeViews.Clear();
        }


        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                m_dragging = true;
                m_lastMousePos = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                m_dragging = false;
            }

            if (!m_dragging) return;

            Vector2 current = Input.mousePosition;
            Vector2 delta = current - m_lastMousePos;
            m_lastMousePos = current;

            _content.anchoredPosition += new Vector2(delta.x, 0);
            Debug.Log(_content.anchoredPosition);
        }


        private float GetMinX()
        {
            float contentWidth = _content.rect.width;
            float viewportWidth = ((RectTransform)_content.parent).rect.width;

            float diff = contentWidth - viewportWidth;

            if (diff <= 0) return 0;

            return -diff / 2f;
        }

        private float GetMaxX()
        {
            float contentWidth = _content.rect.width;
            float viewportWidth = ((RectTransform)_content.parent).rect.width;

            float diff = contentWidth - viewportWidth;

            if (diff <= 0) return 0;

            return diff / 2f;
        }

        private void DrawUILine(RectTransform from, RectTransform to)
        {
            var line = Instantiate(_linePrefab, _lineRoot);

            Vector3 fromPos = from.position;
            Vector3 toPos = to.position;

            Vector3 dir = toPos - fromPos;
            float dist = dir.magnitude;

            line.position = fromPos;

            line.sizeDelta = new Vector2(dist, 6f);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            line.rotation = Quaternion.Euler(0, 0, angle);

            line.SetAsFirstSibling(); // 防止盖住节点
        }
        private Vector2 ClampPosition(Vector2 pos)
        {
            pos.x = Mathf.Clamp(pos.x, GetMinX(), GetMaxX());
            return pos;
        }
    }
}