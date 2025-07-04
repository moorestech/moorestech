using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    public class HudArrow : MonoBehaviour
    {
        [SerializeField] private RectTransform arrowRoot;
        [SerializeField] private RectTransform arrowImageTransform;
        
        [SerializeField] private GameObject arrowImage;
        [SerializeField] private GameObject pointImage;
        
        
        public void SetArrowTransform(Vector2 position, Quaternion rotation, bool isOnScreen)
        {
            arrowRoot.anchoredPosition = position;
            arrowImageTransform.rotation = rotation;
            
            arrowImage.SetActive(!isOnScreen);
            pointImage.SetActive(isOnScreen);
        }
    }
}