using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Tutorial;
using Client.Skit.Context;
using Client.Skit.Define;
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
        
        [Inject] private ISkitActionContext _skitActionContext;
        [Inject] private EnvironmentRoot environmentRoot;
        [Inject] private BlockGameObjectDataStore blockGameObjectDataStore;
        [Inject] private MapObjectPin mapObjectPin;
        
        public bool IsPlayingSkit { get; private set; }
        private bool _isSkip;
        
        private void Awake()
        {
            skitUI.SetActive(false);
        }
        public void Initialize()
        {
            _skitActionContext.OnSkip.Subscribe(_ =>
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
            var commandsToken = (JToken)JsonConvert.DeserializeObject(skitJson.text);
            var commands = CommandForgeLoader.LoadCommands(commandsToken);
            
            //前処理 Pre process
            using var storyContext = await PreProcess();
            CameraManager.RegisterCamera(skitCamera);
            
            foreach (var command in commands)
            {
                try
                {
                    await command.ExecuteAsync(storyContext);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            
            //後処理 Post process
            skitUI.SetActive(false);
            HudArrowManager.SetActive(true);
            mapObjectPin.SetActive(true);
            var characterContainer = storyContext.GetService<CharacterObjectContainer>();
            characterContainer.DestroyAllCharacters();
            IsPlayingSkit = false;
            _isSkip = false;
            CameraManager.UnRegisterCamera(skitCamera);
            
            #region Internal
            
            async UniTask<StoryContext> PreProcess()
            {
                //キャラクターを生成
                var characters = new Dictionary<string, SkitCharacter>();
                
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
                skitUI.SetActive(true);
                mapObjectPin.SetActive(false);
                HudArrowManager.SetActive(false);
                
                // DIコンテナをセットアップ
                var builder = new ContainerBuilder();
                builder.RegisterInstance(skitUI);
                builder.RegisterInstance<ISkitCamera>(skitCamera);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance(new CharacterObjectContainer(characters));
                builder.RegisterInstance<IEnvironmentRoot>(environmentRoot);
                builder.RegisterInstance<IBlockObjectControl>(blockGameObjectDataStore);
                builder.RegisterInstance<ISkitEnvironmentManager>(new SkitEnvironmentManager(transform));
                builder.RegisterInstance(_skitActionContext);
                
                return new StoryContext(builder.Build());
            }
            
            #endregion
        }
    }
}