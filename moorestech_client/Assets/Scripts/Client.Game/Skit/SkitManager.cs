using System;
using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
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
        [Inject] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;
        [Inject] private EntityObjectDatastore entityObjectDatastore;
        [Inject] private IMapObjectPin mapObjectPin;
        [Inject] private IVeinPin veinPin;
        
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
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            var presentationStarted = false;
            var cameraRegistered = false;
            var worldPinsHidden = false;
            SkitLocalizationResolver localizationResolver = null;
            StoryContext storyContext = null;
            CharacterObjectContainer characterContainer = null;

            try
            {
                // 解決器へskitTitleを渡して準備してからコマンドを読み込む
                // Prepare the resolver with the skit title before loading commands
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
                storyContext = await PreProcess();
                CameraManager.RegisterCamera(skitCamera);
                cameraRegistered = true;
                await SkitCommandExecutor.ExecuteAsync(commands, storyContext);
            }
            finally
            {
                Cleanup();
            }
            
            #region Internal
            
            async UniTask<StoryContext> PreProcess()
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
                veinPin.SetActive(false);
                worldPinsHidden = true;

                // DIコンテナをセットアップ
                var builder = new ContainerBuilder();
                builder.RegisterInstance(skitUI);
                builder.RegisterInstance<ISkitCamera>(skitCamera);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance(characterContainer);
                builder.RegisterInstance<ISkitEnvironmentRoot>(environmentRoot);
                builder.RegisterInstance<ISkitBlockObjectControl>(blockGameObjectDataStore);
                builder.RegisterInstance<ISkitMapObjectControl>(mapObjectGameObjectDatastore);
                builder.RegisterInstance<ISkitEntityObjectControl>(entityObjectDatastore);
                builder.RegisterInstance<ISkitEnvironmentManager>(new SkitEnvironmentManager(transform));
                builder.RegisterInstance<ISkitActionContext>(_skitActionController);
                builder.RegisterInstance(new SkitPresentationMode(webUiMode));
                builder.RegisterInstance<ISkitLocalizationResolver>(localizationResolver);

                return new StoryContext(builder.Build());
            }

            void Cleanup()
            {
                // 唯一のfinallyから、実際に変更した再生状態だけを通常状態へ戻す
                // Restore only the playback state actually changed, from the single finally
                skitUI.SetActive(false);
                if (presentationStarted) SkitPresentationStateStore.Instance.End();
                if (worldPinsHidden)
                {
                    mapObjectPin.SetActive(true);
                    veinPin.SetActive(true);
                }
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
