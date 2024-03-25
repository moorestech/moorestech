using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Client.Story
{
    public class StoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject storyPanel;
        
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text storyText;

        [SerializeField] private CanvasGroup transitionImage;

        public void SetText(string characterName,string text)
        {
            storyPanel.SetActive(true);
            characterNameText.text = characterName;
            storyText.text = text;
        }

        public void ShowTransition(bool isShow,float duration)
        {
            transitionImage.alpha = isShow ? 0 : 1;
            transitionImage.DOFade(isShow ? 1 : 0, duration);
        }
    }
}