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
        public List<AnimationInfo> AnimationInfos => animationInfos;
        
        [SerializeField] private List<AnimationInfo> animationInfos = new();
    }
    
    [Serializable]
    public class AnimationInfo
    {
        public string AnimationName => animationInfo;
        public AnimationClip AnimationClip => animationClip;
        
        [SerializeField] private string animationInfo;
        [SerializeField] private AnimationClip animationClip;
    }
}