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
            // JSONの位置はスポーン基準の相対値なので原点を足してワールド座標へ
            // JSON positions are spawn-relative, so add the origin to reach world space
            var origin = storyContext.GetSkitOrigin();
            var isSkip = storyContext.GetService<ISkitActionContext>().IsSkip;
            if (isSkip)
            {
                storyContext.GetSkitCamera().SetTransform(origin.ToWorld(EndPosition), EndRotation);
                return null;
            }
            storyContext.GetSkitCamera().TweenCamera(
                origin.ToWorld(StartPosition),
                StartRotation,
                origin.ToWorld(EndPosition),
                EndRotation,
                Duration,
                (Ease)System.Enum.Parse(typeof(Ease), Easing));
            return null;
        }
    }
}
