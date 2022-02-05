using System;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class EquippedItemViewControl : MonoBehaviour
    {
         Camera _mainCamera;
         RectTransform _canvasRect;
         RectTransform _target;

         [Inject]
         public void Construct(Camera mainCamera)
         {
             _mainCamera = mainCamera;
             _canvasRect = transform.root.GetComponentsInChildren<RectTransform>()[0];
             _target = GetComponent<RectTransform>();
         }

        void Update()
        {
            var magnification = _canvasRect.sizeDelta.x / Screen.width;

            var itemPos = new Vector3();
            
            itemPos.x = Input.mousePosition.x * magnification - _canvasRect.sizeDelta.x / 2;
            itemPos.y = Input.mousePosition.y * magnification - _canvasRect.sizeDelta.y / 2;
            itemPos.z = transform.localPosition.z;

            transform.localPosition = itemPos;
        }
        
        
    }
}