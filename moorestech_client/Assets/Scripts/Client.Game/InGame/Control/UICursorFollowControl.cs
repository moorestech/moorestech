using UnityEngine;

namespace Client.Game.InGame.Control
{
    public class UICursorFollowControl : MonoBehaviour
    {
        [SerializeField] private Vector3 offSet = Vector3.zero;
        private UICursorFollowControlRootCanvasRect _canvasRectRoot;
        
        private void Update()
        {
            if (_canvasRectRoot == null)
            {
                _canvasRectRoot = FindObjectOfType<UICursorFollowControlRootCanvasRect>();
                if (_canvasRectRoot == null) return;
            }
            
            var rectSize = _canvasRectRoot.RectTransform.rect.size;
            var magnification = rectSize.x / Screen.width;
            
            var itemPos = new Vector3();
            
            itemPos.x = UnityEngine.Input.mousePosition.x * magnification - rectSize.x / 2;
            itemPos.y = UnityEngine.Input.mousePosition.y * magnification - rectSize.y / 2;
            itemPos.z = transform.localPosition.z;
            
            transform.localPosition = itemPos + offSet;
        }
    }
}