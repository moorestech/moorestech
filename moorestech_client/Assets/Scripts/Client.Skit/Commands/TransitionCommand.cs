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
            
            SkitPresentationStateStore.Instance.SetTransitionVisible(Enabled);
            await UniTask.Delay((int)(duration * 1000), isSkip);
            return null;
        }
    }
}
