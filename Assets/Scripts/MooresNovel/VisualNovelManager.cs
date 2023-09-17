using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MooresNovel
{
    public class VisualNovelManager : MonoBehaviour
    {
        [SerializeField] private MooresNovelAssets assetsData;
        
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private Image characterImage;

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
        public async UniTask ExecuteVisualNovel(string key)
        {
            var script = assetsData.GetScript(key);
            foreach (var line in script.CreateScripts())
            {
                var character = assetsData.GetCharacter(line.CharacterKey);

                nameText.text = character.Name;
                mainText.text = line.CharacterKey;
                characterImage.sprite = character.CharacterSprite;
                
                //Enterが押されるまで待つ
                await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Return));
            }
        }
        
    }
}