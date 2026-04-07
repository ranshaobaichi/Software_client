using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Utils {
    public class MultilineBestFitText : Text {
        /// <summary>
        /// 当前可见的文字行数
        /// </summary>
        public int VisibleLines { get; private set; }

        private void UseFitSettings() {
            TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
            settings.resizeTextForBestFit = false;

            if (!resizeTextForBestFit) {
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                return;
            }

            int minSize = resizeTextMinSize;
            int txtLen = text.Length;

            //从Best Fit中最大的值开始，逐次递减，每次减小后都尝试生成文本，
            //如果生成的文本可见字符数等于文本内容的长度，则找到满足需求(可以使所有文本都可见的最大字号)的字号。
            for (int i = resizeTextMaxSize; i >= minSize; --i) {
                settings.fontSize = i;
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                if (cachedTextGenerator.characterCountVisible == txtLen) break;
            }
        }

        private readonly UIVertex[] _tmpVerts = new UIVertex[4];

        /// <summary>
        /// 重写绘制顶点方法
        /// </summary>
        /// <param name="toFill"></param>
        protected override void OnPopulateMesh(VertexHelper toFill) {
            if (null == font) return;

            m_DisableFontTextureRebuiltCallback = true;
            UseFitSettings();

            IList<UIVertex> verts = cachedTextGenerator.verts;
            float unitsPerPixel = 1 / pixelsPerUnit;
            int vertCount = verts.Count;

            // 没有要处理的对象时，直接return。
            if (vertCount <= 0) {
                toFill.Clear();
                return;
            }

            Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
            roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
            toFill.Clear();

            for (int i = 0; i < vertCount; ++i) {
                int tempVertsIndex = i & 3;
                _tmpVerts[tempVertsIndex] = verts[i];
                _tmpVerts[tempVertsIndex].position *= unitsPerPixel;
                if (roundingOffset != Vector2.zero) {
                    _tmpVerts[tempVertsIndex].position.x += roundingOffset.x;
                    _tmpVerts[tempVertsIndex].position.y += roundingOffset.y;
                }

                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(_tmpVerts);

            }

            m_DisableFontTextureRebuiltCallback = false;
            VisibleLines = cachedTextGenerator.lineCount;
        }
    }
}