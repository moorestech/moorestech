using Client.Common.Asset;
using Client.Game.Skit.Lifecycle;
using Client.Game.Skit.Localization;
using Client.Game.InGame.UI.UIState;
using Client.Skit.Context;
using Client.Skit.Define;
using Client.Skit.Localization;
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
            var cleanupOnce = new SkitCleanupOnce();
            var presentationStarted = false;
            SkitLocalizationResolver localizationResolver = null;
            StoryContext context = null;

            try
            {
                SkitPresentationStateStore.Instance.BeginBackground();
                presentationStarted = true;

                await UniTask.WaitUntil(() => uiStateControl.CurrentState == UIStateEnum.GameScreen);

                var textAsset = await AddressableLoader.LoadAsyncDefault<TextAsset>(skitAddressablePath);
                if (textAsset == null)
                {
                    Debug.LogError($"背景スキットJSONが見つかりません : {skitAddressablePath}");
                    return;
                }

                var skitTitle = SkitTitle.FromAssetName(textAsset.name);
                localizationResolver = new SkitLocalizationResolver();
                await localizationResolver.PrepareAsync(skitTitle);
                var commandsToken = (JToken)JsonConvert.DeserializeObject(textAsset.text);
                var commands = CommandForgeLoader.LoadCommands(commandsToken);
                context = GetStoryContext(skitTitle);

                backgroundSkitUI.SetActive(true);
                // webモード中はuGUI文字表示のみ抑止する（音声はUnity再生のためルートは維持。SetActive(false)は音声を殺すため禁止）
                // In web mode suppress only the uGUI text; keep the root active because Unity owns voice playback (SetActive(false) would kill audio)
                backgroundSkitUI.SetTextVisible(!WebUiScreenGate.IsWebUiMode);

                // 背景スキットは簡易Text実装
                // Background skits use the minimal text-only implementation
                foreach (var command in commands)
                {
                    await command.ExecuteAsync(context);
                }
            }
            finally
            {
                Cleanup();
            }
            
            #region Internal
            
            StoryContext GetStoryContext(string skitTitle)
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(backgroundSkitUI);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance<ISkitLocalizationResolver>(localizationResolver);
                builder.RegisterInstance(new SkitExecutionIdentity(skitTitle));
                
                return new StoryContext(builder.Build());
            }

            void Cleanup()
            {
                if (!cleanupOnce.TryBegin()) return;

                // 外側finallyから表示・session・DI資源を一度だけ解放する
                // Release UI, session, and DI resources exactly once from the outer finally
                backgroundSkitUI.SetActive(false);
                if (presentationStarted) SkitPresentationStateStore.Instance.End();
                context?.Dispose();
                localizationResolver?.Dispose();
                IsPlayingSkit = false;
            }

            #endregion
        }
        
        public void SetActive(bool isActive)
        {
            backgroundSkitUI.SetActive(isActive);
        }
    }
}
