using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story.StoryTrack
{
    public class TextTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            // TODO ボイス再生とリップシンク
            
            var characterName = parameters[0];
            var text = parameters[1];
            
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