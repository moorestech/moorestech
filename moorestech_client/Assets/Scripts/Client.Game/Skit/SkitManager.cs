using System;
using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
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
        [Inject] private EntityObjectDatastore entityObjectDatastore;
        [Inject] private IReadOnlyList<ITutorialWorldPin> worldPins;
        [Inject] private IReadOnlyList<ISkitWorldObjectControl> worldObjectControls;
        [Inject] private SkitOrigin skitOrigin;
        
        public bool IsPlayingSkit { get; private set; }
        
        // 隠れた会話UIの復帰をuGUI・storeの両バックエンドへ集約し、失敗を握り潰さず呼び出し元へ返す
        // Consolidate hidden-dialogue restoration across both the uGUI and store backends, surfacing failure to the caller instead of swallowing it
        // WebモードではuGUI会話UIは起動しない（SetActive(false)のままStartが走らない）ので、非アクティブ時は非表示扱いにしない
        // In web mode the uGUI dialogue UI never starts (stays SetActive(false)), so an inactive UI never counts as hidden
        public bool TryRestoreHiddenSkitUi()
        {
            var store = SkitPresentationStateStore.Instance;
            var current = store.GetCurrent();
            var isUiHidden = skitUI.gameObject.activeSelf && skitUI.IsUIHidden;
            if (!isUiHidden && !current.PresentationState.UiHidden) return false;

            if (skitUI.gameObject.activeSelf) skitUI.ShowHiddenUI();
            return store.TrySetUiHidden(current.SessionId, current.SceneRevision, false).Ok;
        }
        private bool _isSkip;
        
        // 執筆ツールが本編・SkitTestの双方で同じ原点を引けるよう、シーン上の実体から公開する
        // Expose the origin from the scene instance so authoring tools read the same value in both the main game and SkitTest
        public SkitOrigin GetSkitOrigin()
        {
            return skitOrigin;
        }
        
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
            // ロードのawaitを跨ぐ前に再生中を立て、多重起動が互いの世界復元を奪い合うのを防ぐ
            // Raise the playing flag before the load await, so a double start cannot fight over restoring the world
            IsPlayingSkit = true;

            var storyCsv = await AddressableLoader.LoadAsyncDefault<TextAsset>(addressablePath);
            if (!storyCsv)
            {
                Debug.LogError($"ストーリーCSVが見つかりません : {addressablePath}");
                IsPlayingSkit = false;
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
            var suppressedWorldPins = new List<ITutorialWorldPin>();
            SkitVisibilityLedger visibilityLedger = null;
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
                        skitCharacter.SetSkitOrigin(skitOrigin);
                        characters.Add(characterElement.CharacterId, skitCharacter);
                    }
                    else
                    {
                        Debug.LogError($"キャラクターモデルのロードに失敗しました: {path}");
                    }
                }
                
                // 表示の設定
                skitUI.SetActive(!webUiMode);

                // 抑止できたピンだけを控え、途中で失敗しても解除漏れで消えたままにしない
                // Track only the pins actually suppressed so a mid-way failure cannot leave one hidden forever
                foreach (var worldPin in worldPins)
                {
                    worldPin.BeginSkitSuppress();
                    suppressedWorldPins.Add(worldPin);
                }

                // DIコンテナをセットアップ
                // 位置を書き込む3つのsinkへ原点を押し込み、加算を各コマンドから引き上げる（ADR 0029）
                // Push the origin into the three sinks that write positions, lifting the addition out of every command (ADR 0029)
                skitCamera.SetSkitOrigin(skitOrigin);
                
                var builder = new ContainerBuilder();
                builder.RegisterInstance(skitUI);
                builder.RegisterInstance<ISkitCamera>(skitCamera);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance(characterContainer);
                // 4窓口すべてを台帳経由で公開し、消した窓口の記録と復元を1箇所へ集める
                // Expose all four entry points through the ledger, so recording and restoring live in one place
                visibilityLedger = new SkitVisibilityLedger(
                    environmentRoot,
                    blockGameObjectDataStore,
                    new SkitWorldObjectControlGroup(worldObjectControls),
                    entityObjectDatastore);
                builder.RegisterInstance<ISkitEnvironmentRoot>(visibilityLedger);
                builder.RegisterInstance<ISkitBlockObjectControl>(visibilityLedger);
                builder.RegisterInstance<ISkitWorldObjectControl>(visibilityLedger);
                builder.RegisterInstance<ISkitEntityObjectControl>(visibilityLedger);
                builder.RegisterInstance<ISkitEnvironmentManager>(new SkitEnvironmentManager(transform, skitOrigin));
                builder.RegisterInstance(skitOrigin);
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
                foreach (var suppressedWorldPin in suppressedWorldPins) suppressedWorldPin.EndSkitSuppress();
                visibilityLedger?.RestoreHiddenWindows();
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
