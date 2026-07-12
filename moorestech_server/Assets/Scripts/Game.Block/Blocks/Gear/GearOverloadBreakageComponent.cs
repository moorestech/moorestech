using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;
using Game.Gear.Tick;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    // 過負荷によるブロック破壊を監視するコンポーネント。block単位のUpdateは持たず、GearTickUpdaterのsweepから毎tick駆動される
    // Component monitoring overload block destruction; holds no per-block Update and is driven every tick by the GearTickUpdater sweep
    public class GearOverloadBreakageComponent : IBlockComponent, IGearOverloadTickTarget
    {
        private const int RandomSeed = 19890604;
        private static readonly System.Random SharedRandom = new(RandomSeed);
        private readonly BlockInstanceId _blockInstanceId;
        private readonly IGearEnergyTransformer _gearEnergyTransformer;
        private readonly IGearOverloadParam _overloadParam;
        private readonly uint _checkIntervalTicks;
        private readonly bool _overloadEnabled;
        private uint _elapsedTicks;
        private bool _isDestroyed;

        public GearOverloadBreakageComponent(BlockInstanceId blockInstanceId, IGearEnergyTransformer gearEnergyTransformer, IGearOverloadParam overloadParam)
        {
            _blockInstanceId = blockInstanceId;
            _gearEnergyTransformer = gearEnergyTransformer;
            _overloadParam = overloadParam;
            _checkIntervalTicks = GameUpdater.SecondsToTicks(Math.Max(overloadParam.DestructionCheckInterval, 0.001f));
            _overloadEnabled = overloadParam.BaseDestructionProbability > 0 && (overloadParam.OverloadMaxRpm > 0 || overloadParam.OverloadMaxTorque > 0);

            // 過負荷設定を持つ場合のみsweep対象として登録する
            // Register into the sweep only when overload params are effective
            if (_overloadEnabled) GearNetworkDatastore.RegisterOverloadTickTarget(this);
        }

        public void TickOverloadCheck()
        {
            // 過負荷判定のインターバルを管理
            // Manage interval for overload checks
            if (_isDestroyed) return;

            _elapsedTicks++;
            if (_elapsedTicks < _checkIntervalTicks) return;
            _elapsedTicks = 0;

            // 過負荷時の破壊確率を計算し、抽選する
            // Calculate destruction probability when overloaded and roll
            var chance = CalculateDestructionProbability();
            if (chance <= 0f) return;
            if (SharedRandom.NextDouble() <= chance) RequestRemove();

            #region Internal

            float CalculateDestructionProbability()
            {
                var rpmRatio = _overloadParam.OverloadMaxRpm > 0 ? _gearEnergyTransformer.CurrentRpm.AsPrimitive() / (float)_overloadParam.OverloadMaxRpm : 0f;
                // CurrentTorqueは要求トルク倍率（アイドル低消費）込みであり、アイドル中のgearが破断しにくくなるのは意図した仕様
                // CurrentTorque includes the requested-torque rate (idle reduction), intentionally making idle gears less likely to break
                var torqueRatio = _overloadParam.OverloadMaxTorque > 0 ? _gearEnergyTransformer.CurrentTorque.AsPrimitive() / (float)_overloadParam.OverloadMaxTorque : 0f;
                var rpmExcess = rpmRatio > 1f ? rpmRatio : 0f;
                var torqueExcess = torqueRatio > 1f ? torqueRatio : 0f;
                if (rpmExcess <= 0f && torqueExcess <= 0f) return 0f;

                var multiplier = rpmExcess > 0f && torqueExcess > 0f ? rpmExcess * torqueExcess : Math.Max(rpmExcess, torqueExcess);
                var probability = (float)(_overloadParam.BaseDestructionProbability * multiplier);
                return Mathf.Clamp01(probability);
            }

            void RequestRemove()
            {
                // 破壊はその場で行わず予約する。このtickの計算には最後まで参加し、tick末尾に一括反映される
                // Reserve the destruction instead of applying it in place; the block participates in this tick fully and is removed at tick end
                _isDestroyed = true;
                ServerContext.GetService<IBlockRemovalReservationService>().ReserveRemoval(_blockInstanceId, BlockRemoveReason.Broken);
            }

            #endregion
        }

        public bool IsDestroy => _isDestroyed;

        public void Destroy()
        {
            // 破断経由(RequestRemoveで_isDestroyed済み)でも必ず登録解除する。HashSetのRemoveなので重複解除は無害
            // Always unregister, including the breakage path (where RequestRemove already set _isDestroyed); duplicate removal from the HashSet is harmless
            _isDestroyed = true;
            if (_overloadEnabled) GearNetworkDatastore.UnregisterOverloadTickTarget(this);
        }
    }
}
