using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
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
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.Game.Skit
{
    public class SkitManager : MonoBehaviour
    {
        [SerializeField] private SkitUI skitUI;
        [SerializeField] private SkitCamera skitCamera;
        [SerializeField] private VoiceDefine voiceDefine;
        
        [Inject] private EnvironmentRoot environmentRoot;
        
        public bool IsPlayingSkit { get; private set; }
        
        private void Awake()
        {
            skitUI.SetActive(false);
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
        
        public async UniTask StartSkit(TextAsset skitJson)
        {
            IsPlayingSkit = true;
            var commandsToken = (JToken)JsonConvert.DeserializeObject(skitJson.text);
            var commands = CommandForgeLoader.LoadCommands(commandsToken);
            
            //前処理 Pre process
            using var storyContext = await PreProcess();
            CameraManager.Instance.RegisterCamera(skitCamera);
            
            foreach (var command in commands)
            {
                await command.ExecuteAsync(storyContext);
            }
            
            //後処理 Post process
            skitUI.SetActive(false);
            HudArrowManager.Instance.SetActive(true);
            var characterContainer = storyContext.GetService<CharacterObjectContainer>();
            characterContainer.DestroyAllCharacters();
            IsPlayingSkit = false;
            CameraManager.Instance.UnRegisterCamera(skitCamera);
            
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
                HudArrowManager.Instance.SetActive(false);
                
                // DIコンテナをセットアップ
                var builder = new ContainerBuilder();
                builder.RegisterInstance(skitUI);
                builder.RegisterInstance<ISkitCamera>(skitCamera);
                builder.RegisterInstance(voiceDefine);
                builder.RegisterInstance(new CharacterObjectContainer(characters));
                builder.RegisterInstance<IEnvironmentRoot>(environmentRoot);
                
                return new StoryContext(builder.Build());
            }
            
            #endregion
        }
    }
}