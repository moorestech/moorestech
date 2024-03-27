using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story.StoryTrack
{
    public class TextTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            // TODO ボイス再生とリップシンク
            
            var characterName = parameters[1];
            var text = parameters[2];
            
            storyContext.StoryUI.SetText(characterName, text);
            
            //クリックされるまで待機
            while (true)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    return;
                }
                await UniTask.Yield();
            }
        }
    }
}