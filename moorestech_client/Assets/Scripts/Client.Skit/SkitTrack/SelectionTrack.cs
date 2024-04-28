using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Skit.SkitTrack
{
    public class SelectionTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            storyContext.SkitMainUI.ShowSelectionUI(true);

            var jumpTags = new List<string>();
            var selectionTexts = new List<string>();

            for (var i = 0; i < parameters.Count; i += 2)
            {
                var tag = parameters[i];
                var text = parameters[i + 1];
                //テキストが空文字列だったら終了
                if (text == string.Empty) break;

                jumpTags.Add(tag);
                selectionTexts.Add(text);
            }

            var selectedIndex = await storyContext.SkitMainUI.WaitSelectText(selectionTexts);

            storyContext.SkitMainUI.ShowSelectionUI(false);

            var selectedTag = jumpTags[selectedIndex];
            if (selectedTag == string.Empty)
            {
                return null;
            }

            return selectedTag;
        }
    }
}