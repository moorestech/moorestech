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
        [SerializeField] private EffectSettings[] effectSettings;
        
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
            
            foreach (var effectSetting in effectSettings)
            {
                var effectValue = Mathf.Lerp(effectSetting.MinValue, effectSetting.MaxValue, effectSetting.RateCurve.Evaluate(rpmRate));
                effectSetting.VisualEffect.SetFloat(effectSetting.Name, effectValue);
            }
        }
    }
    
    [Serializable]
    public class EffectSettings
    {
        public string Name;
        
        public VisualEffect VisualEffect;
        public float MinValue;
        public float MaxValue;
        public AnimationCurve RateCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }
}