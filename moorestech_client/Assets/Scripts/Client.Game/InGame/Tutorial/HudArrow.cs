using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    public class HudArrow : MonoBehaviour
    {
        [SerializeField] private RectTransform arrowRoot;
        [SerializeField] private RectTransform arrowImageTransform;
        
        [SerializeField] private GameObject arrowImage;
        [SerializeField] private GameObject pointImage;
        
        private GameObject _target;
        private HudArrowOptions _options;
        
        public void Initialize(GameObject target, HudArrowOptions options)
        {
            _target = target;
            _options = options;
        }
        
        public void ManualUpdate()
        {
            if (_options.HideWhenTargetInactive && _target)
            {
                gameObject.SetActive(_target.activeInHierarchy);
            }
        }
        
        public void SetArrowTransform(Vector2 position, Quaternion rotation, bool isOnScreen)
        {
            arrowRoot.anchoredPosition = position;
            arrowImageTransform.rotation = rotation;
            
            arrowImage.SetActive(!isOnScreen);
            pointImage.SetActive(isOnScreen);
        }
    }
}