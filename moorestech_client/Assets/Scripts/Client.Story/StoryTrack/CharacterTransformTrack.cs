using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Story.StoryTrack
{
    public class CharacterTransformTrack : IStoryTrack
    {
        public async UniTask ExecuteTrack(StoryContext storyContext, string[] parameters)
        {
            var characterKey = parameters[1];
            
            var posX = float.Parse(parameters[3]);
            var posY = float.Parse(parameters[4]);
            var posZ = float.Parse(parameters[5]);
            var pos = new Vector3(posX, posY, posZ);
            
            var rotX = float.Parse(parameters[7]);
            var rotY = float.Parse(parameters[8]);
            var rotZ = float.Parse(parameters[9]);
            var rot = new Vector3(rotX, rotY, rotZ);
            
            var character = storyContext.GetCharacter(characterKey);
            
            character.SetTransform(pos,rot);
        }
    }
}