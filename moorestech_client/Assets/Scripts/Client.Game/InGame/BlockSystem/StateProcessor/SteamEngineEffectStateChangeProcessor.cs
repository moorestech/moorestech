using System;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class SteamEngineEffectStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private VisualEffect visualEffect;
        
        [SerializeField] private float rateMaxValue = 25;
        [SerializeField] private AnimationCurve rateCurve;
        
        [SerializeField] private float rateSizeValue = 1.25f;
        [SerializeField] private AnimationCurve sizeCurve;
        
        private SteamGearGeneratorBlockParam _blockParam;
        private GearStateDetail _currentGearState;
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockParam = (SteamGearGeneratorBlockParam)blockGameObject.BlockMasterElement.BlockParam;
        }
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }
        
        private void Update()
        {
            if (_currentGearState == null) return;
            
            var rpmRate = _currentGearState.CurrentRpm / _blockParam.GenerateMaxRpm;
            
            var rate = rateCurve.Evaluate(rpmRate) * rateMaxValue;
            var size = sizeCurve.Evaluate(rpmRate) * rateSizeValue;
            
            visualEffect.SetFloat("Rate", rate);
            visualEffect.SetFloat("Size", size);
        }
    }
}