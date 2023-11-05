using MainGame.Basic.UI;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    public interface IRectTransformHighlightObject
    {
        public bool IsTargetDestroyed { get; }
        public void Destroy();
    }

    public class RectTransformHighlightObject : MonoBehaviour, IRectTransformHighlightObject
    {
        /// <summary>
        ///     ハイライトの画像のTransform
        /// </summary>
        [SerializeField] private RectTransform highlightImage;

        /// <summary>
        ///     ハイライトを置く場所である<see cref="RectTransformReadonlyData" />
        /// </summary>
        private RectTransformReadonlyData _targetHighlightRectTransform;

        private void Update()
        {
            if (!_targetHighlightRectTransform.IsDestroyed) _targetHighlightRectTransform.SyncRectTransform(highlightImage);
        }

        public bool IsTargetDestroyed => _targetHighlightRectTransform.IsDestroyed;

        public void Destroy()
        {
            Debug.Log("Destory");
            Destroy(gameObject);
        }


        public void Init(RectTransformReadonlyData targetHighlightRectTransform)
        {
            _targetHighlightRectTransform = targetHighlightRectTransform;
            _targetHighlightRectTransform.SyncRectTransform(highlightImage);
        }
    }
}