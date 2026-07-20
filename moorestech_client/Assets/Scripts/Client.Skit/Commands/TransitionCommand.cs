using System.Threading;
using Client.Skit.Context;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class TransitionCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var isSkip = storyContext.GetService<ISkitActionContext>().IsSkip;
            var duration = Duration;
            if (isSkip)
            {
                duration = 0;
            }
            
            var mode = storyContext.GetService<SkitPresentationMode>();
            if (mode.WebUiEnabled)
                SkitPresentationStateStore.Instance.SetTransitionVisible(Enabled);
            else
                storyContext.GetSkitUI().ShowTransition(Enabled, duration);
            await UniTask.Delay((int)(duration * 1000), isSkip);
            return null;
        }
    }
}
