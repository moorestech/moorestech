using System;
using UnityEngine;

namespace Client.Story
{
    public class StoryCharacter : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer faceSkinnedMeshRenderer;
        [SerializeField] private Animator animator;

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
            animator.Play(animationName);
        }
        
        public void SetEmotion(EmotionType emotion, float duration)
        {
            
            
            
            #region Internal
            
            int ToBlendShapeIndex(EmotionType emotionType)
            {
                return emotionType switch
                {
                    EmotionType.Normal => 0,
                    EmotionType.Happy => 1,
                    EmotionType.Sad => 2,
                    EmotionType.Angry => 3,
                    EmotionType.Surprised => 4,
                    _ => throw new System.ArgumentOutOfRangeException()
                };
            }

            #endregion
        }
    }
}