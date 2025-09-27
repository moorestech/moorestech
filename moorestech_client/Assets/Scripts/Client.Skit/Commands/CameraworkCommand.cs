using System.Threading;
using Client.Skit.Context;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace CommandForgeGenerator.Command
{
    public partial class CameraworkCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var isSkip = storyContext.GetService<ISkitActionContext>().IsSkip;
            if (isSkip)
            {
                storyContext.GetSkitCamera().SetTransform(EndPosition, EndRotation);
                return null;
            }
            storyContext.GetSkitCamera().TweenCamera(
                StartPosition,
                StartRotation,
                EndPosition,
                EndRotation,
                Duration,
                (Ease)System.Enum.Parse(typeof(Ease), Easing));
            return null;
        }
    }
}
