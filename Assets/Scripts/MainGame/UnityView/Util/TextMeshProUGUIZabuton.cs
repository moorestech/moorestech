using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.Util
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextMeshProUGUIZabuton : MonoBehaviour
    {
        private const float Tolerance = 0.00001f;
        
        [SerializeField] private Image image;
        [SerializeField] private float paddingWidth;
        [SerializeField] private float paddingHeight;

        private TextMeshProUGUI _tmp;
        private float _preWidth;
        private float _preHeight;

        private void Start()
        {
            _tmp = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (Math.Abs(_preWidth - _tmp.preferredWidth) < Tolerance && Math.Abs(_preHeight - _tmp.preferredHeight) < Tolerance) return;

            UpdateTMProUGUISizeDelta();
            UpdateImageSizeDelta();
        }

        /// <summary>
        /// RectTransform.sizeDeltaをテキストにぴっちりさせる
        /// </summary>
        private void UpdateTMProUGUISizeDelta()
        {
            _preWidth = _tmp.preferredWidth;
            _preHeight = _tmp.preferredHeight;
            _tmp.rectTransform.sizeDelta = new Vector2(_preWidth, _preHeight);
        }
        
        /// <summary>
        /// 背景のImageのRectTransform.sizeDeltaを指定したパディングで更新
        /// </summary>
        private void UpdateImageSizeDelta()
        {
            if (_preHeight == 0 || _preWidth == 0)
            {
                image.rectTransform.sizeDelta = Vector2.zero;
                return;
            }

            image.rectTransform.sizeDelta = new Vector2(_preWidth + paddingWidth, _preHeight + paddingHeight);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            Update();
        }
#endif
    }
}