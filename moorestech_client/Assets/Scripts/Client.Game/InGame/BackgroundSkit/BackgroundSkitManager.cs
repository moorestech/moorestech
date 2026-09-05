using Client.Common.Asset;
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
        
        [SerializeField] private BackgroundSkitVoicePlayer backgroundSkitVoicePlayer;
        [SerializeField] private UIStateControl uiStateControl;
        
        [SerializeField] private VoiceDefine voiceDefine;
        
        [Inject] private SkitOrigin skitOrigin;
        
        public async UniTask StartBackgroundSkit(string skitAddressablePath)
        {
            IsPlayingSkit = true;
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
                context = GetStoryContext();

                backgroundSkitVoicePlayer.SetActive(true);

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
            
            StoryContext GetStoryContext()
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(backgroundSkitVoicePlayer);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance<ISkitLocalizationResolver>(localizationResolver);
                // SkitManagerと同型に実原点を素通しし、背景スキットだけ別の座標系になる余地を残さない（ADR 0029）
                // Pass the real origin through just like SkitManager, leaving no room for background skits to use a different coordinate space (ADR 0029)
                builder.RegisterInstance(skitOrigin);

                return new StoryContext(builder.Build());
            }

            void Cleanup()
            {
                // 唯一のfinallyから表示・session・DI資源を解放する
                // Release UI, session, and DI resources from the single finally
                backgroundSkitVoicePlayer.SetActive(false);
                if (presentationStarted) SkitPresentationStateStore.Instance.End();
                context?.Dispose();
                localizationResolver?.Dispose();
                IsPlayingSkit = false;
            }

            #endregion
        }
        
        public void SetActive(bool isActive)
        {
            backgroundSkitVoicePlayer.SetActive(isActive);
        }
    }
}
