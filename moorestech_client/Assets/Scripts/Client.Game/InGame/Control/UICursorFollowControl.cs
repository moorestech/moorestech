using UnityEngine;

namespace Client.Game.InGame.Control
{
    public class UICursorFollowControl : MonoBehaviour
    {
        [SerializeField] private Vector3 offSet = Vector3.zero;
        [SerializeField] private RectTransform canvasRect;
        
        private void Update()
        {
            var magnification = canvasRect.sizeDelta.x / Screen.width;
            
            var itemPos = new Vector3();
            
            itemPos.x = UnityEngine.Input.mousePosition.x * magnification - canvasRect.sizeDelta.x / 2;
            itemPos.y = UnityEngine.Input.mousePosition.y * magnification - canvasRect.sizeDelta.y / 2;
            itemPos.z = transform.localPosition.z;
            
            transform.localPosition = itemPos + offSet;
        }
    }
}