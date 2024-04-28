using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.UI.Inventory.HotBar
{
    public class ProgressBarView : MonoBehaviour
    {
        private const float FadeTime = 0.1f;

        [SerializeField] private Slider slider;
        [SerializeField] private CanvasGroup canvasGroup;

        private float _lastSetTime;

        private void Update()
        {
            canvasGroup.alpha = _lastSetTime + FadeTime < Time.time ? 0 : 1;
        }

        /// <summary>
        ///     TODO 採掘中　みたいなステートをちゃんと管理して、同時に採掘しないようにする
        /// </summary>
        /// <param name="progress"></param>
        public void SetProgress(float progress)
        {
            _lastSetTime = Time.time;
            slider.value = progress;
        }
    }
}