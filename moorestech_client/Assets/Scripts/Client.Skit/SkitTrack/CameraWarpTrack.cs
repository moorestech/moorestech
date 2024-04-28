using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story.StoryTrack
{
    public class CameraWarpTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var pos = new Vector3(float.Parse(parameters[1]), float.Parse(parameters[2]), float.Parse(parameters[3]));
            var rot = new Vector3(float.Parse(parameters[5]), float.Parse(parameters[6]), float.Parse(parameters[7]));

            storyContext.SkitCamera.SetTransform(pos, rot);

            return null;
        }
    }
}