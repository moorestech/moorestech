using UnityEngine;

namespace Client.Game.InGame.Control
{
    public class UICursorFollowControlRootCanvasRect : MonoBehaviour
    {
        public RectTransform RectTransform => rectTransform;
        [SerializeField] private RectTransform rectTransform;
        
    }
}