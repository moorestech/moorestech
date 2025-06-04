using System.Collections.Generic;
using Client.Common;
using Client.Common.Asset;
using Client.Skit.Define;
using Client.Skit.Skit;
using Client.Skit.UI;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.Skit
{
    public class SkitManager : MonoBehaviour
    {
        [SerializeField] private SkitUI skitUI;
        
        [SerializeField] private SkitCamera skitCamera;
        
        [SerializeField] private CharacterDefine characterDefine;
        [SerializeField] private VoiceDefine voiceDefine;
        
        public bool IsPlayingSkit { get; private set; }
        
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
            var storyContext = PreProcess();
            CameraManager.Instance.RegisterCamera(skitCamera);
            
            foreach (var command in commands)
            {
                await command.ExecuteAsync(storyContext);
            }
            
            //後処理 Post process
            skitUI.gameObject.SetActive(false);
            storyContext.DestroyCharacter();
            IsPlayingSkit = false;
            CameraManager.Instance.UnRegisterCamera(skitCamera);
            
            #region Internal
            
            StoryContext PreProcess()
            {
                //キャラクターを生成
                var characters = new Dictionary<string, SkitCharacter>();
                foreach (var characterInfo in characterDefine.CharacterInfos)
                {
                    var character = Instantiate(characterInfo.CharacterPrefab);
                    character.Initialize(transform, characterInfo.CharacterKey);
                    characters.Add(characterInfo.CharacterKey, character);
                }
                
                // 表示の設定
                skitUI.gameObject.SetActive(true);
                
                return new StoryContext(skitUI, characters, skitCamera, voiceDefine);
            }
            
            #endregion
        }
    }
}