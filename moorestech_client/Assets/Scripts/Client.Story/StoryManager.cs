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
                character.Initialize(transform, characterInfo.CharacterKey);
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
                var trackKey = values[0];

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

                await track.ExecuteTrack(storyContext, values);
            }

            //後処理
            storyCamera.SetEnabled(false);
            if (cameraController) cameraController.SetEnable(true);
        }
    }
}