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
        [SerializeField] private CharacterDefine characterDefine;
        [SerializeField] private TextAsset storyCsv;
        
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
            var storyContext = new StoryContext(storyUI, characters, storyCamera);
            
            storyCamera.SetEnabled(true);
            CameraController.Instance.SetEnable(false);
            
            // CSVを1行ずつ読んで処理をする
            var lines = storyCsv.text.Split('\n');
            foreach (var line in lines)
            {
                var values = line.Split(',');
                var trackKey = values[0];
                
                var track = StoryTrackDefine.GetStoryTrack(trackKey);

                await track.ExecuteTrack(storyContext, values);
            }
            
            //後処理
            storyCamera.SetEnabled(false);
            CameraController.Instance.SetEnable(true);
        }
    }
}