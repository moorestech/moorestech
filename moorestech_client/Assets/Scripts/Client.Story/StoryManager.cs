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
            var characters = new Dictionary<string, StoryCharacter>();
            foreach (var characterInfo in characterDefine.CharacterInfos)
            {
                var character = Instantiate(characterInfo.CharacterPrefab);
                character.Initialize(transform);
                characters.Add(characterInfo.CharacterKey, character);
            }

            var storyContext = new StoryContext(storyUI, characters, storyCamera, voiceDefine);

            storyCamera.SetEnabled(true);
            if (cameraController) cameraController.SetEnable(false);

            // CSVを1行ずつ読んで処理をする
            var lines = storyCsv.text.Split('\n');
            foreach (var line in lines)
            {
                var values = line.Split(',');
                var trackValue = values[0];
                var trackKey = values[1];

                if (trackKey == "End")
                {
                    break;
                }

                Debug.Log($"トラックを実行 : {trackKey}\nパラメータ : {string.Join(", ", values)}");
                var track = StoryTrackDefine.GetStoryTrack(trackKey);
                if (track == null)
                {
                    Debug.LogError($"トラックが見つかりません : {trackKey}\nパラメータ : {string.Join(", ", values)}");
                    continue;
                }
                
                var parameters = new List<string>();
                for (var i = 2; i < values.Length; i++)
                {
                    parameters.Add(values[i]);
                }

                await track.ExecuteTrack(storyContext, parameters);
            }

            //後処理
            storyCamera.SetEnabled(false);
            if (cameraController) cameraController.SetEnable(true);
        }
    }
}