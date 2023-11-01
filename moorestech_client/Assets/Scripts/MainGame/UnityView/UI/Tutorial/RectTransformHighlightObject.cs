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
        /// ハイライトの画像のTransform
        /// </summary>
        [SerializeField] private RectTransform highlightImage;
        
        /// <summary>
        /// ハイライトを置く場所である<see cref="RectTransformReadonlyData"/>
        /// </summary>
        private RectTransformReadonlyData _targetHighlightRectTransform;
        
        public bool IsTargetDestroyed => _targetHighlightRectTransform.IsDestroyed;


        public void Init(RectTransformReadonlyData targetHighlightRectTransform)
        {
            _targetHighlightRectTransform = targetHighlightRectTransform;
            _targetHighlightRectTransform.SyncRectTransform(highlightImage);
        }
        
        private void Update()
        {
            if (!_targetHighlightRectTransform.IsDestroyed)
            {
                _targetHighlightRectTransform.SyncRectTransform(highlightImage);
            }
        }

        public void Destroy()
        {
            Debug.Log("Destory");
            Object.Destroy(gameObject);
        }
    }
}