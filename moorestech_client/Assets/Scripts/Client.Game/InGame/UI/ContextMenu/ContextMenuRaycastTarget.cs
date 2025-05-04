using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.ContextMenu
{
    public class ContextMenuRaycastTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public bool PointerStay { get; private set; }
        
        #region flagController
        
        public void OnPointerMove(PointerEventData eventData)
        {
            PointerStay = true;
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerStay = true;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            PointerStay = false;
        }
        
        private void OnDestroy()
        {
            PointerStay = false;
        }
        
        private void OnDisable()
        {
            PointerStay = false;
        }
        
        #endregion
    }
}