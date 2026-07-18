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
            SkitPresentationStateStore.Instance.BeginBackground();
            
            // UIステートがGameScreenになるまで待機
            await UniTask.WaitUntil(() => uiStateControl.CurrentState == UIStateEnum.GameScreen);
            
            var textAsset = await AddressableLoader.LoadAsyncDefault<TextAsset>(skitAddressablePath);
            var commandsToken = (JToken)JsonConvert.DeserializeObject(textAsset.text);
            var commands = CommandForgeLoader.LoadCommands(commandsToken);
            var context = GetStoryContext();
            
            backgroundSkitUI.SetActive(true);
            // webモード中はuGUI文字表示のみ抑止する（音声はUnity再生のためルートは維持。SetActive(false)は音声を殺すため禁止）
            // In web mode suppress only the uGUI text; keep the root active because Unity owns voice playback (SetActive(false) would kill audio)
            backgroundSkitUI.SetTextVisible(!WebUiScreenGate.IsWebUiMode);

            // BackgroundSkitは簡易実装なので、Textコマンドのみを実行
            foreach (var command in commands)
            {
                await command.ExecuteAsync(context);
            }
            
            backgroundSkitUI.SetActive(false);
            SkitPresentationStateStore.Instance.End();
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
