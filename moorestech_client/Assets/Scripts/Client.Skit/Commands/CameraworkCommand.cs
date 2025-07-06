using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CameraworkCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
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
