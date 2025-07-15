using System.Linq;
using Client.Skit.Context;
using Client.Skit.SkitObject;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public partial class SkitObjectControlCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var skitObjects = Object.FindObjectsOfType<SkitControllableObject>();
            var targetObject = skitObjects.FirstOrDefault(obj => obj.ObjectId == SkitObjectId);
            
            if (targetObject == null)
            {
                Debug.LogWarning($"SkitControllableObject with ID '{SkitObjectId}' not found.");
                return null;
            }
            
            if (Action == "SetActive")
            {
                targetObject.SetActive(ActiveEnable);
            }
            
            return null;
        }
    }
}