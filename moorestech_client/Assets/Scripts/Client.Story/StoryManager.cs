using System.Collections.Generic;
using Client.Game.Control.MouseKeyboard;
using Client.Story.StoryTrack;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story
{
    public class StoryManager : MonoBehaviour
    {
        [SerializeField] private StoryUI storyUI;
        [SerializeField] private StoryCamera storyCamera;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private TextAsset storyCsv;

        [SerializeField] private CharacterDefine characterDefine;
        [SerializeField] private VoiceDefine voiceDefine;

        public async UniTask StartStory()
        {
            //前処理
            var storyContext = PreProcess();
            var lines = storyCsv.text.Split('\n');
            var tagIndexTable = CreateTagIndexTable(storyCsv.text.Split('\n'));

            for (var i = 0; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');

                var trackKey = values[1];

                if (trackKey == "End") break;

                var track = StoryTrackDefine.GetStoryTrack(trackKey);
                if (track == null)
                {
                    Debug.LogError($"トラックが見つかりません : {trackKey}\nパラメータ : {string.Join(", ", values)}");
                    break;
                }

                var parameters = CreateParameter(values);
                var nextTag = await track.ExecuteTrack(storyContext, parameters);
                if (nextTag == null) continue;
                
                var nextIndex = GetTagIndex(lines, nextTag);
                if (nextIndex == -1)
                {
                    Debug.LogError($"次のタグが見つかりません : トラック : {trackKey} 当該タグ : {nextTag}\nパラメータ : {string.Join(", ", values)}");
                    break;
                }
                i = nextIndex - 1;
            }

            //後処理
            storyCamera.SetEnabled(false);
            if (cameraController) cameraController.SetEnable(true);

            #region Internal

            StoryContext PreProcess()
            {
                //キャラクターを生成
                var characters = new Dictionary<string, StoryCharacter>();
                foreach (var characterInfo in characterDefine.CharacterInfos)
                {
                    var character = Instantiate(characterInfo.CharacterPrefab);
                    character.Initialize(transform);
                    characters.Add(characterInfo.CharacterKey, character);
                }

                //カメラの設定
                storyCamera.SetEnabled(true);
                if (cameraController) cameraController.SetEnable(false);

                return new StoryContext(storyUI, characters, storyCamera, voiceDefine);
            }

            List<string> CreateParameter(string[] values)
            {
                var parameters = new List<string>();
                for (var j = 2; j < values.Length; j++)
                {
                    parameters.Add(values[j]);
                }

                return parameters;
            }
            
            Dictionary<string,int> CreateTagIndexTable(string[] lines)
            {
                var tagIndex = new Dictionary<string, int>();
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var values = line.Split(',');

                    if (values.Length < 2) continue;
                    var tag = values[0];
                    tagIndex.Add(tag, i);
                }

                return tagIndex;
            }

            int GetTagIndex(string[] lines, string tag)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var values = line.Split(',');

                    if (values[0] == tag)
                    {
                        return i;
                    }
                }

                return -1;
            }

            #endregion
        }
    }
}