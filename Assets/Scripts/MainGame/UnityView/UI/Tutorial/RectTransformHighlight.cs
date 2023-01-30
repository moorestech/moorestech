using MainGame.Basic.UI;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    public class RectTransformHighlight : MonoBehaviour
    {
        [SerializeField] private RectTransform highlightPrefab;
        public RectTransformHighlightObject CreateHighlightObject(RectTransformReadonlyData rectTransform)
        {
            var highlight = Instantiate(highlightPrefab, transform);
            return new RectTransformHighlightObject(highlight,rectTransform);
        }
    }

    public class RectTransformHighlightObject
    {
        /// <summary>
        /// 自分の<see cref="RectTransform"/>
        /// </summary>
        private readonly RectTransform _rectTransform;
        
        /// <summary>
        /// ハイライトを置く場所である<see cref="RectTransformReadonlyData"/>
        /// </summary>
        private readonly RectTransformReadonlyData _targetHighlightRectTransform;


        public RectTransformHighlightObject(RectTransform rectTransform, RectTransformReadonlyData targetHighlightRectTransform)
        {
            _rectTransform = rectTransform;
            _targetHighlightRectTransform = targetHighlightRectTransform;
            
            UpdateObject();
        }
        
        public void UpdateObject()
        {
            _targetHighlightRectTransform.SyncRectTransform(_rectTransform);
        }

        public void Destroy()
        {
            GameObject.Destroy(_rectTransform.gameObject);
        }
    } 
}