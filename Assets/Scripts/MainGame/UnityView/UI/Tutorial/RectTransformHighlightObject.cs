using MainGame.Basic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Tutorial
{
    public interface IRectTransformHighlightObject
    {
        public void Destroy();
        public bool IsTargetDestroyed { get; }
    }
    public class RectTransformHighlightObject : MonoBehaviour,IRectTransformHighlightObject
    {
        /// <summary>
        /// ハイライトの画像
        /// </summary>
        [SerializeField] private Image highlightImage;
        private RectTransform _highlightRectTransform; 
        
        /// <summary>
        /// ハイライトを置く場所である<see cref="RectTransformReadonlyData"/>
        /// </summary>
        private RectTransformReadonlyData _targetHighlightRectTransform;
        
        public bool IsTargetDestroyed => _targetHighlightRectTransform.IsDestroyed;


        public void Init(RectTransformReadonlyData targetHighlightRectTransform)
        {
            _highlightRectTransform = highlightImage.rectTransform;
            _targetHighlightRectTransform = targetHighlightRectTransform;
        }
        
        private void Update()
        {
            _targetHighlightRectTransform.SyncRectTransform(_highlightRectTransform);
        }

        public void Destroy()
        {
            Object.Destroy(gameObject);
        }
    }
}