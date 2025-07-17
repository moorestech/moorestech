using Client.Skit.Context;
using UnityEngine;

namespace CommandForgeGenerator.Command.Util
{
    public class CameraUtil
    {
        public static (Vector3, Vector3) GetCameraTransform(StoryContext storyContext ,string cameraOrigin, Vector3 position, Vector3 rotation, string cameraOriginCharacter, string cameraOriginBone)
        {
            if (cameraOrigin == "absolute")
            {
                return (position, rotation);
            }
            if (cameraOrigin == "characterBone")
            {
                var character = storyContext.GetCharacter(cameraOriginCharacter);
                return character.GetBoneAbsoluteTransform(cameraOriginBone);
            }
            
            return (Vector3.zero, Vector3.zero);
        }
    }
}