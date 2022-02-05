using System;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class EquippedItemViewControl : MonoBehaviour
    {
         Camera _mainCamera;
         RectTransform _parent;
         RectTransform _target;

         [Inject]
         public void Construct(Camera mainCamera)
         {
             _mainCamera = mainCamera;
             _parent = transform.parent.GetComponent<RectTransform>();
             _target = GetComponent<RectTransform>();
         }

        void Update()
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent,
                Input.mousePosition ,
                _mainCamera,
                out var mousePos);
            
            _target.anchoredPosition = new Vector2(
                mousePos.x,
                mousePos.y);
        }
    }
}