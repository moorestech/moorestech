using System;
using System.Collections.Generic;
using Client.Skit.Skit;
using UnityEngine;
using UnityEngine.Serialization;

namespace Client.Skit.Define
{
    [CreateAssetMenu(fileName = "AnimationDefine", menuName = "moorestech/AnimationDefine", order = 0)]
    public class AnimationDefine : ScriptableObject
    {
        [SerializeField] private List<AnimationInfo> animationInfos = new();
        
        public AnimationClip GetAnimationClip(string animationId)
        {
            return animationInfos.Find(animationInfo => animationInfo.AnimationId == animationId)?.AnimationClip;
        }
    }
    
    [Serializable]
    public class AnimationInfo
    {
        public string AnimationId => animationId;
        public AnimationClip AnimationClip => animationClip;
        
        [SerializeField] private string animationId;
        [SerializeField] private AnimationClip animationClip;
    }
}