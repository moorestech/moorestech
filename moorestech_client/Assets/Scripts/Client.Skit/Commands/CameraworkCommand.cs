using Client.Skit.Context;
using CommandForgeGenerator.Command.Util;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class CameraworkCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var (startPos, startRot) = CameraUtil.GetCameraTransform(storyContext, StartCameraOrigin, StartPosition, StartRotation, StartCameraOriginCharacter, StartCameraOriginBone);
            var (endPos, endRot) = CameraUtil.GetCameraTransform(storyContext, EndCameraOrigin, EndPosition, EndRotation, EndCameraOriginCharacter, EndCameraOriginBone);

            storyContext.GetSkitCamera().TweenCamera(
                startPos,
                startRot,
                endPos,
                endRot,
                Duration,
                (Ease)System.Enum.Parse(typeof(Ease), Easing));
            return null;
        }
    }
}
