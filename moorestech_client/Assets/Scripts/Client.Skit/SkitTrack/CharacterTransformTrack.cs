using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Skit.SkitTrack
{
    public class CharacterTransformTrack : IStoryTrack
    {
        public async UniTask<string> ExecuteTrack(StoryContext storyContext, List<string> parameters)
        {
            var characterKey = parameters[0];

            var posX = float.Parse(parameters[2]);
            var posY = float.Parse(parameters[3]);
            var posZ = float.Parse(parameters[4]);
            var pos = new Vector3(posX, posY, posZ);

            var rotX = float.Parse(parameters[6]);
            var rotY = float.Parse(parameters[7]);
            var rotZ = float.Parse(parameters[8]);
            var rot = new Vector3(rotX, rotY, rotZ);

            var character = storyContext.GetCharacter(characterKey);

            character.SetTransform(pos, rot);

            return null;
        }
    }
}