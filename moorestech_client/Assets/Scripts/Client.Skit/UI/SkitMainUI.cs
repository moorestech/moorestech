using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Client.Story.UI
{
    public class SkitMainUI : MonoBehaviour
    {
        [SerializeField] private GameObject storyPanel;

        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text storyText;

        [SerializeField] private CanvasGroup transitionImage;

        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private List<SelectionButton> selectionButtons;

        public void SetText(string characterName, string text)
        {
            storyPanel.SetActive(true);
            characterNameText.text = characterName;
            storyText.text = text;
        }

        public void ShowTransition(bool isShow, float duration)
        {
            transitionImage.alpha = isShow ? 0 : 1;
            transitionImage.DOFade(isShow ? 1 : 0, duration);
        }

        public void ShowSelectionUI(bool enable)
        {
            selectionPanel.SetActive(enable);
        }

        public async UniTask<int> WaitSelectText(List<string> texts)
        {
            for (int i = 0; i < selectionButtons.Count; i++)
            {
                if (i < texts.Count)
                {
                    selectionButtons[i].SetButton(texts[i], i);
                    selectionButtons[i].SetActive(true);
                }
                else
                {
                    selectionButtons[i].SetActive(false);
                }
            }

            var cancelToken = this.GetCancellationTokenOnDestroy();

            var (_, resultIndex) = await UniTask.WhenAny(selectionButtons.Select(button => button.WaitClick(cancelToken)));

            return resultIndex;
        }
    }
}