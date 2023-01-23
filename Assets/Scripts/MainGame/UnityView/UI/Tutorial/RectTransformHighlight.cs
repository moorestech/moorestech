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
            rectTransform.SyncRectTransform(highlight);
            return new RectTransformHighlightObject(highlight.gameObject);
        }
    }

    public class RectTransformHighlightObject
    {
        private readonly GameObject _gameObject;
        
        public RectTransformHighlightObject(GameObject gameObject)
        {
            _gameObject = gameObject;
        }
        public void Destroy()
        {
            GameObject.Destroy(_gameObject);
        }
    } 
}