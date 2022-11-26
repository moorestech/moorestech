using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class ProgressBarView : MonoBehaviour
    {
        private const float FadeTime = 0.1f;

        [SerializeField] private Slider slider;
        [SerializeField] private CanvasGroup canvasGroup;

        private float _lastSetTime;
        
        public void SetProgress(float progress)
        {
            _lastSetTime = Time.time;
            slider.value = progress;
        }

        private void Update()
        {
            canvasGroup.alpha = _lastSetTime + FadeTime < Time.time ? 0 : 1;
        }
    }
}