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
            
            transform.localPosition = GetLocalPosition(_canvasRectRoot, transform.localPosition, offSet);
        }
        
        public static Vector3 GetLocalPosition(UICursorFollowControlRootCanvasRect canvasRectRoot, Vector3 currentLocalPosition, Vector3 offset)
        {
            
            var rectSize = canvasRectRoot.RectTransform.rect.size;
            var magnification = rectSize.x / Screen.width;
            
            var itemPos = new Vector3
                {
                    x = UnityEngine.Input.mousePosition.x * magnification - rectSize.x / 2,
                    y = UnityEngine.Input.mousePosition.y * magnification - rectSize.y / 2,
                    z = currentLocalPosition.z,
                };
            
            return itemPos + offset;
        }
    }
}