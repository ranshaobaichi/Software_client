using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Utils {
    public class Toast : MonoBehaviour {
        private const float StartX = 400f;
        private const float TargetX = 0f;
        private const float MoveDuration = 2.5f;
        private const float StayDuration = 1.5f;
        private static WaitForSeconds s_stayWait = new WaitForSeconds(StayDuration);
        
        [SerializeField]
        private Text _text;
        
        public void SetText(string text) {
            _text.text = text;
        }

        /// <summary>
        /// Move in from right to center,
        /// then wait for a while,
        /// then destroy itself.
        /// </summary>
        public void MoveIn() {
            StartCoroutine(MoveInAction());
        }

        private IEnumerator MoveInAction() {
            var rect = transform as RectTransform;
            if (!rect) {
                yield break;
            }
            
            var anchoredPos = rect.anchoredPosition;
            anchoredPos.x = StartX;
            rect.anchoredPosition = anchoredPos;

            var elapsed = 0f;
            while (elapsed < MoveDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / MoveDuration);

                // Ease-out cubic for a quick start and gentle stop.
                var easedT = 1f - Mathf.Pow(1f - t, 3f);
                var x = Mathf.Lerp(StartX, TargetX, easedT);

                // Keep x within [targetX, startX] and never pass 0.
                var clampedX = Mathf.Clamp(x, TargetX, StartX);
                
                anchoredPos = rect.anchoredPosition;
                anchoredPos.x = clampedX;
                rect.anchoredPosition = anchoredPos;
                
                yield return null;
            }

            anchoredPos = rect.anchoredPosition;
            anchoredPos.x = TargetX;
            rect.anchoredPosition = anchoredPos;

            yield return s_stayWait;
            Destroy(gameObject);
        }
    }
}