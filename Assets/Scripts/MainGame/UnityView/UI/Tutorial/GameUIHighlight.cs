using System;
using System.Collections.Generic;
using MainGame.Basic.UI;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    public class GameUIHighlight : MonoBehaviour
    {
        [SerializeField] private RectTransformHighlight rectTransformHighlight;

        [SerializeField] private RectTransform craftItemPutButton;
        private readonly Dictionary<HighlightType, RectTransformHighlightObject> _rectTransformHighlightObjects = new Dictionary<HighlightType, RectTransformHighlightObject>();

        public void SetHighlight(HighlightType highlightType,bool isActive)
        {
            var isExist = _rectTransformHighlightObjects.TryGetValue(highlightType, out var highlightObject);

            switch (isExist)
            {
                //ハイライトがない場合でオンにする場合は作成
                case false when isActive:
                {
                    RectTransform rectTransform = highlightType switch
                    {
                        HighlightType.CraftItemPutButton => craftItemPutButton,
                        _ => null
                    };

                    _rectTransformHighlightObjects[highlightType] = rectTransformHighlight.CreateHighlightObject(new RectTransformReadonlyData(rectTransform));
                
                    return;
                }
                //ハイライトがあって、オフにする場合は削除
                case true when !isActive:
                    highlightObject.Destroy();
                    _rectTransformHighlightObjects.Remove(highlightType);
                    return;
            }
        }

        private void Update()
        {
            foreach (var retTransformObjects in _rectTransformHighlightObjects.Values)
            {
                retTransformObjects.UpdateObject();
            }
        }
    }
    
    public enum HighlightType
    {
        CraftItemPutButton,
    }
}