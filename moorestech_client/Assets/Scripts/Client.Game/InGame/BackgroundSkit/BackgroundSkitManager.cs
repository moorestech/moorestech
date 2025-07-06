using Client.Common.Asset;
using Client.Game.InGame.UI.UIState;
using Client.Skit.Define;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;

namespace Client.Game.InGame.BackgroundSkit
{
    public class BackgroundSkitManager : MonoBehaviour
    {
        [SerializeField] private BackgroundSkitUI backgroundSkitUI;
        [SerializeField] private UIStateControl uiStateControl;
        
        [SerializeField] private VoiceDefine voiceDefine;
        
        public async UniTask StartBackgroundSkit(string skitAddressablePath)
        {
            // UIステートがGameScreenになるまで待機
            await UniTask.WaitUntil(() => uiStateControl.CurrentState == UIStateEnum.GameScreen);
            
            var textAsset = await AddressableLoader.LoadAsyncDefault<TextAsset>(skitAddressablePath);
            var commandsToken = (JToken)JsonConvert.DeserializeObject(textAsset.text);
            var commands = CommandForgeLoader.LoadCommands(commandsToken);
            var characterMaster = MasterHolder.CharacterMaster;
            
            backgroundSkitUI.SetActive(true);
            
            // BackgroundSkitは簡易実装なので、Textコマンドのみを実行
            foreach (var command in commands)
            {
                await command.ExecuteAsync();
            }
            
            backgroundSkitUI.SetActive(false);
        }
        
        public void SetActive(bool isActive)
        {
            backgroundSkitUI.SetActive(isActive);
        }
    }
}