using Client.Game.InGame.UI.UIState;
using Client.Skit.Define;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BackgroundSkit
{
    public class BackgroundSkitManager : MonoBehaviour
    {
        [SerializeField] private BackgroundSkitUI backgroundSkitUI;
        [SerializeField] private UIStateControl uiStateControl;
        
        [SerializeField] private VoiceDefine voiceDefine;
        
        public async UniTask StartBackgroundSkit(TextAsset storyCsv)
        {
            // UIステートがGameScreenになるまで待機
            await UniTask.WaitUntil(() => uiStateControl.CurrentState == UIStateEnum.GameScreen);
            
            backgroundSkitUI.SetActive(true);
            
            var lines = storyCsv.text.Split('\n');
            
            foreach (var line in lines)
            {
                var values = line.Split(',');
                var characterName = values[0];
                var text = values[1].Replace("\\n", "\n");
                
                var voice = voiceDefine.GetVoiceClip(characterName, text);
                
                await backgroundSkitUI.SetText(characterName, text, voice);
            }
            
            backgroundSkitUI.SetActive(false);
        }
    }
}