using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using static System.Enum;

namespace Client.Story.StoryTrack
{
    public class CameraworkTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var duration = float.Parse(parameters[1]);
            var easing = (Ease)Parse(typeof(Ease), parameters[2]);
            
            var fromPos = new Vector3(float.Parse(parameters[4]), float.Parse(parameters[5]), float.Parse(parameters[6]));
            var fromRot = new Vector3(float.Parse(parameters[8]), float.Parse(parameters[9]), float.Parse(parameters[10]));
            
            var toPos = new Vector3(float.Parse(parameters[12]), float.Parse(parameters[13]), float.Parse(parameters[14]));
            var toRot = new Vector3(float.Parse(parameters[16]), float.Parse(parameters[17]), float.Parse(parameters[18]));
            
            storyContext.StoryCamera.TweenCamera(fromPos, fromRot, toPos, toRot, duration, easing);
        }
    }
}