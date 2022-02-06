using System;
using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class EquippedItemViewControl : MonoBehaviour
    {
         RectTransform _canvasRect;

         [Inject]
         public void Construct()
         {
             _canvasRect = transform.root.GetComponentsInChildren<RectTransform>()[0];
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