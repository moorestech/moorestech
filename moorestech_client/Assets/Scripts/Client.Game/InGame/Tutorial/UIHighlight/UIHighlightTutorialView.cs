using TMPro;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UIHighlightTutorialView : MonoBehaviour, ITutorialView
    {
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private RectTransform highlightImage;
        [SerializeField] private TMP_Text highlightText;
        
        private UIHighlightTutorialTargetObject _highlightTutorialTargetObject;
        
        public void SetTargetObject(UIHighlightTutorialTargetObject tutorialTargetObject, string text)
        {
            _highlightTutorialTargetObject = tutorialTargetObject;
            highlightText.text = text;
        }
        
        private void Update()
        {
            SyncRectTransform();
        }
        
        private void SyncRectTransform()
        {
            highlightObject.SetActive(_highlightTutorialTargetObject.ActiveSelf);
            
            //一旦親を変更し、また親を戻すことによって、ローカル座標を正しく反映することができる
            var currentParent = highlightImage.parent;
            var targetRect = _highlightTutorialTargetObject.RectTransform;
            highlightImage.SetParent(targetRect.parent);
            
            //変更した上で、データを反映する
            highlightImage.position = targetRect.position;
            highlightImage.rotation = targetRect.rotation;
            highlightImage.localScale = targetRect.localScale;
            
            highlightImage.pivot = targetRect.pivot;
            highlightImage.anchoredPosition = targetRect.anchoredPosition;
            highlightImage.anchorMax = targetRect.anchorMax;
            highlightImage.anchorMin = targetRect.anchorMin;
            highlightImage.offsetMax = targetRect.offsetMax;
            highlightImage.offsetMin = targetRect.offsetMin;
            highlightImage.sizeDelta = targetRect.sizeDelta;
            highlightImage.anchoredPosition3D = targetRect.anchoredPosition3D;
            
            //元の親に戻す
            highlightImage.SetParent(currentParent);
        }
        
        public void CompleteTutorial()
        {
            Destroy(gameObject);
        }
    }
}