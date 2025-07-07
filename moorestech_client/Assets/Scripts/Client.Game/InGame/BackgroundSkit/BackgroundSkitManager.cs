using Client.Common.Asset;
using Client.Game.InGame.UI.UIState;
using Client.Skit.Context;
using Client.Skit.Define;
using Client.Skit.UI;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.BackgroundSkit
{
    public class BackgroundSkitManager : MonoBehaviour
    {
        public bool IsPlayingSkit { get; private set; }
        
        [SerializeField] private BackgroundSkitUI backgroundSkitUI;
        [SerializeField] private UIStateControl uiStateControl;
        
        [SerializeField] private VoiceDefine voiceDefine;
        
        public async UniTask StartBackgroundSkit(string skitAddressablePath)
        {
            IsPlayingSkit = true;
            
            // UIステートがGameScreenになるまで待機
            await UniTask.WaitUntil(() => uiStateControl.CurrentState == UIStateEnum.GameScreen);
            
            var textAsset = await AddressableLoader.LoadAsyncDefault<TextAsset>(skitAddressablePath);
            var commandsToken = (JToken)JsonConvert.DeserializeObject(textAsset.text);
            var commands = CommandForgeLoader.LoadCommands(commandsToken);
            var context = GetStoryContext();
            
            backgroundSkitUI.SetActive(true);
            
            // BackgroundSkitは簡易実装なので、Textコマンドのみを実行
            foreach (var command in commands)
            {
                await command.ExecuteAsync(context);
            }
            
            backgroundSkitUI.SetActive(false);
            IsPlayingSkit = false;
            
            #region Internal
            
            StoryContext GetStoryContext()
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(backgroundSkitUI);
                builder.RegisterInstance(voiceDefine);
                
                return new StoryContext(builder.Build());
            }
            
  #endregion
        }
        
        public void SetActive(bool isActive)
        {
            backgroundSkitUI.SetActive(isActive);
        }
    }
}