using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Algorithms {
    [TestFixture]
    public class MapGeneratorTests {
        private const int DefaultBatchRuns = 1000;

        private class MapGeneratorPrototype {
            public enum NodeType {
                Normal,
                Elite,
                Event,
                Boss
            }

            public class MapNode {
                public NodeType type;
                public int column;
                public int rowInColumn;
                public List<MapNode> nextNodes = new List<MapNode>();
                public float difficulty;
            }

            public class TowerMap {
                public List<List<MapNode>> columns;
                public MapNode startNode;
                public MapNode bossNode;
            }

            public int minCol = 12, maxCol = 15;
            public int minRow = 1, maxRow = 3;

            private NodeType GetRandomType(int col, int totalCol) {
                float p = (float)col / totalCol;
                int r = Random.Range(0, 10);
                if (p < 0.3f) return r < 8 ? NodeType.Normal : NodeType.Event;
                if (p < 0.7f) return r < 5 ? NodeType.Normal : r < 8 ? NodeType.Elite : NodeType.Event;
                return r < 4 ? NodeType.Normal : r < 8 ? NodeType.Elite : NodeType.Event;
            }

            public TowerMap GenerateMap() {
                TowerMap map = new TowerMap();
                map.columns = new List<List<MapNode>>();

                int columnCount = Random.Range(minCol, maxCol + 1);

                // --- 1. 生成所有节点 ---
                for (int col = 0; col < columnCount; col++) {
                    // 最后一列固定只生成一个 Boss 节点
                    int rowCount = col == columnCount - 1 ? 1 : Random.Range(minRow, maxRow + 1);
                    List<MapNode> column = new List<MapNode>();

                    for (int row = 0; row < rowCount; row++) {
                        MapNode node = new MapNode();
                        node.column = col;
                        node.rowInColumn = row;

                        // 最后一列强制 Boss
                        if (col == columnCount - 1)
                            node.type = NodeType.Boss;
                        else
                            node.type = GetRandomType(col, columnCount);

                        // 难度曲线（线性递增）
                        node.difficulty = GetDifficulty(col, columnCount);

                        column.Add(node);
                    }

                    map.columns.Add(column);
                }

                // --- 2. 连接路径 ---
                ConnectPaths(map);

                // --- 3. 设置起点 & Boss ---
                map.startNode = map.columns[0][Random.Range(0, map.columns[0].Count)];
                map.bossNode = map.columns[columnCount - 1][0]; // 最后一列默认只有一个

                return map;
            }

            private void ConnectPaths(TowerMap map) {
                for (int col = 0; col < map.columns.Count - 1; col++) {
                    var currCol = map.columns[col];
                    var nextCol = map.columns[col + 1];

                    var path = GetRandomValidPathFast(currCol.Count, nextCol.Count);

                    for (int i = 0; i < currCol.Count; i++) {
                        var (s, e) = path[i];

                        for (int j = s; j <= e; j++) {
                            currCol[i].nextNodes.Add(nextCol[j]);
                        }
                    }
                }
            }

            private List<(int start, int end)> GetRandomValidPathFast(int outputCount, int inputCount) {
                var result = new List<(int start, int end)>();

                int lastEnd = 0;

                for (int i = 0; i < outputCount; i++) {
                    int remainingOutputs = outputCount - i - 1;

                    int minStart = lastEnd;
                    int maxStart = Mathf.Min(lastEnd + 1, inputCount - 1);

                    int start = Random.Range(minStart, maxStart + 1);

                    int minEnd = start;
                    int maxEnd = inputCount - 1 - remainingOutputs;

                    int end = Random.Range(minEnd, maxEnd + 1);

                    result.Add((start, end));
                    lastEnd = end;
                }

                return result;
            }

            private float GetDifficulty(int col, int totalCol) {
                float t = (float)col / (totalCol - 1);

                // 可调曲线：前期平缓，后期陡增
                return Mathf.Lerp(1f, 10f, t * t);
            }
        }

        private bool GenerateMap_ValidPaths(MapGeneratorPrototype.TowerMap map) {
            for (int col = 0; col < map.columns.Count - 1; col++) {
                var currCol = map.columns[col];
                var nextCol = map.columns[col + 1];

                int minNextIndex = 0; // 后续节点索引必须保持非递减，避免路径交叉
                foreach (var node in currCol) {
                    if (node.nextNodes == null || node.nextNodes.Count == 0) {
                        return false;
                    }

                    int curMaxNextIndex = minNextIndex;
                    foreach (var nextNode in node.nextNodes) {
                        if (nextNode == null) {
                            return false;
                        }

                        // nextNode 必须来自下一列，且行索引合法
                        if (nextNode.column != col + 1 || nextNode.rowInColumn < 0 || nextNode.rowInColumn >= nextCol.Count) {
                            return false;
                        }

                        if (nextNode.rowInColumn < minNextIndex) {
                            return false; // 路径交叉
                        }

                        curMaxNextIndex = Mathf.Max(nextNode.rowInColumn, curMaxNextIndex);
                    }

                    minNextIndex = curMaxNextIndex;
                }
            }

            return true;
        }
        
        public void GenerateMap_NoIntersectingPaths() {
            var generator = new MapGeneratorPrototype();
            var map = generator.GenerateMap();

            Assert.IsNotNull(map);
            Assert.IsNotNull(map.columns);
            Assert.IsTrue(map.columns.Count >= generator.minCol && map.columns.Count <= generator.maxCol);
            Assert.IsTrue(GenerateMap_ValidPaths(map));
        }

        // TestCase(totalRuns, progressStepPercent)
        // totalRuns: 批量运行总次数
        // progressStepPercent: 进度日志输出步长（百分比）
        [TestCase(DefaultBatchRuns, 20)]
        [TestCase(200, 20)]
        [TestCase(50, 10)]
        public void GenerateMap_NoIntersectingPaths_BatchRun(int totalRuns, int progressStepPercent) {
            totalRuns = Mathf.Max(1, totalRuns);
            int progressStep = Mathf.Clamp(progressStepPercent, 1, 100);

            var generator = new MapGeneratorPrototype();
            int failedRuns = 0;
            int nextProgress = progressStep;

            for (int i = 1; i <= totalRuns; i++) {
                bool isValid;
                try {
                    var map = generator.GenerateMap();
                    isValid = map != null
                        && map.columns != null
                        && map.columns.Count >= generator.minCol
                        && map.columns.Count <= generator.maxCol
                        && GenerateMap_ValidPaths(map);
                }
                catch {
                    isValid = false;
                }

                if (!isValid) {
                    failedRuns++;
                }

                int percent = i * 100 / totalRuns;
                while (percent >= nextProgress) {
                    TestContext.Progress.WriteLine($"Map batch progress: {nextProgress}% ({i}/{totalRuns})");
                    nextProgress += progressStep;
                }
            }

            if (nextProgress <= 100) {
                TestContext.Progress.WriteLine($"Map batch progress: 100% ({totalRuns}/{totalRuns})");
            }

            int passedRuns = totalRuns - failedRuns;
            float passRate = passedRuns * 100f / totalRuns;
            TestContext.Progress.WriteLine($"Map batch done. total={totalRuns}, failed={failedRuns}, passRate={passRate:F2}%");

            // 不中途停止；在全部执行完后统一给出失败结果。
            Assert.That(failedRuns, Is.EqualTo(0),
                $"Batch run completed with failures. total={totalRuns}, failed={failedRuns}, passRate={passRate:F2}%");
        }
    }
}