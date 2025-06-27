using System;
using Client.Skit.Define;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace Client.Skit.Skit
{
    public class SkitCharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private AnimationDefine _animationDefine;
        
        private PlayableGraph _playableGraph;
        private AnimationClipPlayable _clipPlayable;
        private AnimationMixerPlayable _mixerPlayable;
        private string _currentAnimationId;
        
        private float _mixerDuration;
        private float _currentMixerDuration;
        
        public void Initialize(AnimationDefine animationDefine)
        {
            _animationDefine = animationDefine;  
        }
        
        public void PlayAnimation(string animationId, float mixerDuration)
        {
            if (_currentAnimationId == animationId)
            {
                _clipPlayable.SetTime(0);
                _clipPlayable.SetDone(false);
            }
            
            Stop();
            
            _mixerDuration = mixerDuration;
            _currentMixerDuration = 0;
            _currentAnimationId = animationId;
            
            var animationClip = _animationDefine.GetAnimationClip(animationId);
            
            _playableGraph = PlayableGraph.Create("SkitCharacterAnimator");
            var oldClipPlayable = _clipPlayable;
            _clipPlayable = AnimationClipPlayable.Create(_playableGraph, animationClip);
            
            _mixerPlayable = AnimationMixerPlayable.Create(_playableGraph, 2);
            _mixerPlayable.ConnectInput(0, _clipPlayable, 0);
            _mixerPlayable.ConnectInput(1, oldClipPlayable, 0);
            
            var playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", animator);
            
            playableOutput.SetSourcePlayable(_mixerPlayable);
            
            #region Internal
            
            void Stop()
            {
                if(_playableGraph.IsValid())
                {
                    _playableGraph.Destroy();
                    _playableGraph = default;
                }
            }
            
            #endregion
        }
        
        private void Update()
        {
            if (!_playableGraph.IsValid()) return;
            
            _playableGraph.Evaluate();
            _currentMixerDuration += Time.deltaTime;
            
            var weight = Mathf.Clamp01(_currentMixerDuration / _mixerDuration);
            
            _mixerPlayable.SetInputWeight(0, 1 - weight);
            _mixerPlayable.SetInputWeight(1, weight);
        }
        
        private void OnDestroy()
        {
            if (!_playableGraph.IsValid()) return;
            
            _playableGraph.Destroy();
            _playableGraph = default;
        }
    }
}