using System;
using MainGame.Basic.UI;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    public class RectTransformHighlightCreator : MonoBehaviour
    {
        [SerializeField] private RectTransformHighlightObject highlightPrefab;
        public IRectTransformHighlightObject CreateHighlightObject(RectTransformReadonlyData rectTransform)
        {
            var highlight = Instantiate(highlightPrefab, transform);
            highlight.Init(rectTransform);
            return highlight;
        }
    }
}