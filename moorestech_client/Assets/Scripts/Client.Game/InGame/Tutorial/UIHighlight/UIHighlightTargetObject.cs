using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    [RequireComponent(typeof(RectTransform))]
    public class UIHighlightTargetObject : MonoBehaviour
    {
        public bool ActiveSelf => gameObject.activeSelf;
        
        public string HighlightObjectId => highlightObjectId;
        [SerializeField] private string highlightObjectId;
        
        public RectTransform RectTransform => rectTransform;
        [SerializeField] private RectTransform rectTransform;
    }
}