using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.MooresNovel.ScriptData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.MooresNovel
{
    public class VisualNovelManager : MonoBehaviour
    {
        [SerializeField] private MooresNovelScript scriptData;
        [SerializeField] private MooresNovelCharacter characterData;
        
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private Image characterImage;

        public async UniTask ExecuteVisualNovel(string key)
        {
            var script = scriptData.GetScript(key);
            foreach (var line in script.Scripts)
            {
                var character = characterData.GetCharacter(line.CharacterKey);

                nameText.text = character.Name;
                mainText.text = line.CharacterKey;
                characterImage.sprite = character.CharacterSprite;
                
                //Enterが押されるまで待つ
                await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Return));
            }
        }
        
    }
}