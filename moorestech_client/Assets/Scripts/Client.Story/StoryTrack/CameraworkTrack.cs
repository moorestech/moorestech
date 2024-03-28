using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using static System.Enum;

namespace Client.Story.StoryTrack
{
    public class CameraworkTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var duration = float.Parse(parameters[0]);
            var easing = (Ease)Parse(typeof(Ease), parameters[1]);
            
            var fromPos = new Vector3(float.Parse(parameters[3]), float.Parse(parameters[4]), float.Parse(parameters[5]));
            var fromRot = new Vector3(float.Parse(parameters[7]), float.Parse(parameters[8]), float.Parse(parameters[9]));
            
            var toPos = new Vector3(float.Parse(parameters[11]), float.Parse(parameters[12]), float.Parse(parameters[13]));
            var toRot = new Vector3(float.Parse(parameters[15]), float.Parse(parameters[16]), float.Parse(parameters[17]));
            
            storyContext.StoryCamera.TweenCamera(fromPos, fromRot, toPos, toRot, duration, easing);
            
            return null;
        }
    }
}