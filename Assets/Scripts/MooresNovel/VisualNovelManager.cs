using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MooresNovel
{
    public class VisualNovelManager : MonoBehaviour
    {
        [SerializeField] private MooresNovelAssets assetsData;
        
        [SerializeField] private Image transitionImage;
        
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private Image characterImage;

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
        public async UniTask ExecuteVisualNovel(string key)
        {
            var script = assetsData.GetScenario(key);
            foreach (var novelEvent in script.CreateScenario())
            {
                switch (novelEvent.EventType)
                {
                    case MooresNovelEventType.Line:
                        var line = (MooresNovelLine)novelEvent;
                        var character = assetsData.GetCharacter(line.CharacterKey);

                        nameText.text = character.Name;
                        mainText.text = line.Text.Replace("\\n", "\n");
                        characterImage.sprite = character.CharacterSprite;

                        //Enterが押されるまで待つ
                        await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Return));
                        break;
                    case MooresNovelEventType.Transition:
                        transitionImage.gameObject.SetActive(true);
                        await UniTask.Delay(2000);
                        transitionImage.gameObject.SetActive(false);
                        await UniTask.Delay(1000);
                        break;
                }
            }
        }
        
    }
}