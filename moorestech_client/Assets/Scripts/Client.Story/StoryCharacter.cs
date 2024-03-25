using UnityEngine;

namespace Client.Story
{
    public class StoryCharacter : MonoBehaviour
    {
        
        public void Initialize(Transform parent)
        {
            transform.SetParent(parent);
        }
        
        public void SetTransform(Vector3 position, Vector3 rotation)
        {
            transform.position = position;
            transform.eulerAngles = rotation;
        }
        
        public void PlayAnimation(string animationName)
        {
            // Play animation
        }
    }
}