using System;
using Client.Skit.Define;
using UnityEngine;

namespace Client.Skit.Skit
{
    public class SkitCharacterDebug : MonoBehaviour
    {
        [SerializeField] private SkitCharacterAnimator _skitCharacterAnimator;
        [SerializeField] private AnimationDefine _animationDefine;
        
        [SerializeField] private string _animationId;
        [SerializeField] private float _miexrDuration;
        
        private void Awake()
        {
            _skitCharacterAnimator.Initialize(_animationDefine);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A) && !string.IsNullOrEmpty(_animationId))
            {
                _skitCharacterAnimator.PlayAnimation(_animationId, _miexrDuration);
            }
        }
    }
}