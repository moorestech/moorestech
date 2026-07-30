using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Game.Skit.Lifecycle;
using Client.Game.Skit.Localization;
using Client.Skit.Context;
using Client.Skit.Define;
using Client.Skit.Localization;
using Client.Skit.Skit;
using Client.Skit.UI;
using CommandForgeGenerator.Command;
using Core.Master;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.Game.Skit
{
    public class SkitManager : MonoBehaviour, IInitializable
    {
        [SerializeField] private SkitUI skitUI;
        [SerializeField] private SkitCamera skitCamera;
        [SerializeField] private VoiceDefine voiceDefine;
        
        [Inject] private ISkitActionController _skitActionController;
        [Inject] private EnvironmentRoot environmentRoot;
        [Inject] private BlockGameObjectDataStore blockGameObjectDataStore;
        [Inject] private IMapObjectPin mapObjectPin;
        
        public bool IsPlayingSkit { get; private set; }
        private bool _isSkip;
        
        private void Awake()
        {
            skitUI.SetActive(false);
        }
        public void Initialize()
        {
            _skitActionController.OnSkip.Subscribe(_ =>
            {
                _isSkip = true;
            }).AddTo(this);
        }
        
        public async UniTask StartSkit(string addressablePath)
        {
            var storyCsv = await AddressableLoader.LoadAsyncDefault<TextAsset>(addressablePath);
            if (!storyCsv)
            {
                Debug.LogError($"ストーリーCSVが見つかりません : {addressablePath}");
                return;
            }
            
            await StartSkit(storyCsv);
        }
        
        private async UniTask StartSkit(TextAsset skitJson)
        {
            IsPlayingSkit = true;
            _isSkip = false;
            var cleanupOnce = new SkitCleanupOnce();
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            var presentationStarted = false;
            var cameraRegistered = false;
            SkitLocalizationResolver localizationResolver = null;
            StoryContext storyContext = null;
            CharacterObjectContainer characterContainer = null;

            try
            {
                // 解決器の準備後にコマンドを読み込み、同じ実行identityへ束縛する
                // Prepare localization before loading commands and bind them to one execution identity
                var skitTitle = SkitTitle.FromAssetName(skitJson.name);
                localizationResolver = new SkitLocalizationResolver();
                await localizationResolver.PrepareAsync(skitTitle);
                var commandsToken = (JToken)JsonConvert.DeserializeObject(skitJson.text);
                var commands = CommandForgeLoader.LoadCommands(commandsToken);

                if (webUiMode)
                {
                    SkitPresentationStateStore.Instance.BeginBlocking(_skitActionController);
                    presentationStarted = true;
                }

                // 前処理で生成物を捕捉し、途中失敗でもfinallyから破棄できるようにする
                // Capture pre-process resources so finally can dispose them after partial failure
                storyContext = await PreProcess(skitTitle);
                CameraManager.RegisterCamera(skitCamera);
                cameraRegistered = true;
                await SkitCommandExecutor.ExecuteAsync(commands, storyContext);
            }
            finally
            {
                Cleanup();
            }
            
            #region Internal
            
            async UniTask<StoryContext> PreProcess(string skitTitle)
            {
                //キャラクターを生成
                var characters = new Dictionary<string, SkitCharacter>();
                characterContainer = new CharacterObjectContainer(characters);
                
                // CharacterMasterから全キャラクター情報を取得
                var characterMaster = MasterHolder.CharacterMaster;
                foreach (var characterElement in characterMaster.ChallengeMasterElements)
                {
                    // Addressableからキャラクターモデルをロード
                    var path = characterElement.SkitModelAddresablePath;
                    var characterPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(path);
                    if (characterPrefab != null)
                    {
                        var characterInstance = Instantiate(characterPrefab);
                        var skitCharacter = characterInstance.GetComponent<SkitCharacter>();
                        skitCharacter.Initialize(transform);
                        characters.Add(characterElement.CharacterId, skitCharacter);
                    }
                    else
                    {
                        Debug.LogError($"キャラクターモデルのロードに失敗しました: {path}");
                    }
                }
                
                // 表示の設定
                skitUI.SetActive(!webUiMode);
                mapObjectPin.SetActive(false);

                // DIコンテナをセットアップ
                var builder = new ContainerBuilder();
                builder.RegisterInstance(skitUI);
                builder.RegisterInstance<ISkitCamera>(skitCamera);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance(characterContainer);
                builder.RegisterInstance<ISkitEnvironmentRoot>(environmentRoot);
                builder.RegisterInstance<ISkitBlockObjectControl>(blockGameObjectDataStore);
                builder.RegisterInstance<ISkitEnvironmentManager>(new SkitEnvironmentManager(transform));
                builder.RegisterInstance<ISkitActionContext>(_skitActionController);
                builder.RegisterInstance(new SkitPresentationMode(webUiMode));
                builder.RegisterInstance<ISkitLocalizationResolver>(localizationResolver);
                builder.RegisterInstance(new SkitExecutionIdentity(skitTitle));
                
                return new StoryContext(builder.Build());
            }

            void Cleanup()
            {
                if (!cleanupOnce.TryBegin()) return;

                // 外側finallyから全ての再生状態を一度だけ通常状態へ戻す
                // Restore every playback state exactly once from the outer finally
                skitUI.SetActive(false);
                if (presentationStarted) SkitPresentationStateStore.Instance.End();
                mapObjectPin.SetActive(true);
                characterContainer?.DestroyAllCharacters();
                if (cameraRegistered) CameraManager.UnRegisterCamera(skitCamera);
                storyContext?.Dispose();
                localizationResolver?.Dispose();
                IsPlayingSkit = false;
                _isSkip = false;
            }
            
            #endregion
        }
    }
}
