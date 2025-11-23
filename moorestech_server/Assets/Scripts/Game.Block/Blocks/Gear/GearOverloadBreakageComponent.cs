using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    // 過負荷によるブロック破壊を監視するコンポーネント
    // Component that monitors block destruction due to overload
    public class GearOverloadBreakageComponent : IUpdatableBlockComponent
    {
        private readonly BlockInstanceId _blockInstanceId;
        private readonly IGearEnergyTransformer _gearEnergyTransformer;
        private readonly GearOverloadConfig _overloadConfig;
        private readonly double _checkInterval;
        private readonly bool _overloadEnabled;
        private double _elapsedSeconds;
        private bool _isDestroyed;

        public GearOverloadBreakageComponent(BlockInstanceId blockInstanceId, IGearEnergyTransformer gearEnergyTransformer, GearOverloadConfig overloadConfig)
        {
            _blockInstanceId = blockInstanceId;
            _gearEnergyTransformer = gearEnergyTransformer;
            _overloadConfig = overloadConfig;
            _checkInterval = Math.Max(overloadConfig.DestructionCheckInterval, 0.001f);
            _overloadEnabled = overloadConfig.IsActive;
        }

        public void Update()
        {
            // 過負荷判定のインターバルを管理
            // Manage interval for overload checks
            if (!_overloadEnabled || _isDestroyed) return;

            _elapsedSeconds += GameUpdater.UpdateSecondTime;
            if (_elapsedSeconds < _checkInterval) return;
            _elapsedSeconds = 0;

            // 過負荷時の破壊確率を計算し、抽選する
            // Calculate destruction probability when overloaded and roll
            var chance = CalculateDestructionProbability();
            if (chance <= 0f) return;
            if (UnityEngine.Random.value <= chance) RequestRemove();

            #region Internal

            float CalculateDestructionProbability()
            {
                var rpmRatio = _overloadConfig.OverloadMaxRpm > 0 ? _gearEnergyTransformer.CurrentRpm.AsPrimitive() / (float)_overloadConfig.OverloadMaxRpm : 0f;
                var torqueRatio = _overloadConfig.OverloadMaxTorque > 0 ? _gearEnergyTransformer.CurrentTorque.AsPrimitive() / (float)_overloadConfig.OverloadMaxTorque : 0f;
                var rpmExcess = rpmRatio > 1f ? rpmRatio : 0f;
                var torqueExcess = torqueRatio > 1f ? torqueRatio : 0f;
                if (rpmExcess <= 0f && torqueExcess <= 0f) return 0f;

                var multiplier = rpmExcess > 0f && torqueExcess > 0f ? rpmExcess * torqueExcess : Math.Max(rpmExcess, torqueExcess);
                var probability = (float)(_overloadConfig.BaseDestructionProbability * multiplier);
                return Mathf.Clamp01(probability);
            }

            void RequestRemove()
            {
                _isDestroyed = true;
                ServerContext.WorldBlockDatastore.RemoveBlock(_blockInstanceId, BlockRemoveReason.Broken);
            }

            #endregion
        }
    }
}

