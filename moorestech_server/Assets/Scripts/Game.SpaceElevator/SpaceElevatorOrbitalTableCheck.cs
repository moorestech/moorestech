using System;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.Orbit;
using UnityEngine;

namespace Game.SpaceElevator
{
    /// <summary>
    ///     起動時に拠点のマスタを軌道到着の表と突き合わせ、合わなければ起動を止める（ADR 0089）
    ///     At boot, checks base masters against the orbital arrival table and stops the boot on a mismatch (ADR 0089)
    /// </summary>
    public class SpaceElevatorOrbitalTableCheck : IBootInitializable
    {
        private readonly IOrbitalArrivalService _orbitalArrivalService;

        public SpaceElevatorOrbitalTableCheck(IOrbitalArrivalService orbitalArrivalService)
        {
            _orbitalArrivalService = orbitalArrivalService;
        }

        public void Load()
        {
            foreach (var (blockName, param) in SpaceElevatorBaseMasters.All())
            {
                ThrowIfNotOrbitalPayload(blockName, param.CableItemGuid);
                foreach (var payloadGuid in param.CarriedPayloadItemGuids)
                {
                    ThrowIfNotOrbitalPayload(blockName, payloadGuid);
                    ThrowIfResultNeverFits(blockName, payloadGuid, param.OutputSlotCount);
                }
            }

            #region Internal

            void ThrowIfNotOrbitalPayload(string blockName, Guid itemGuid)
            {
                if (_orbitalArrivalService.IsOrbitalPayload(MasterHolder.ItemMaster.GetItemId(itemGuid))) return;
                Fail($"宇宙エレベーター拠点 {blockName} のアイテム {itemGuid} が軌道到着の表（items の orbitalPayloads）にありません");
            }

            // 衛星1機の成果が空の出口にも入らないと、昇り切っても永遠に渡せない
            // If one satellite's result cannot fit even into empty outlets, the climber could never hand it over
            void ThrowIfResultNeverFits(string blockName, Guid payloadGuid, int outputSlotCount)
            {
                var payload = MasterHolder.ItemMaster.Items.OrbitalPayloads.Single(row => row.PayloadItemGuid == payloadGuid);
                if (payload.CountPerLaunch != 1) Fail($"宇宙エレベーター拠点 {blockName} の積荷 {payloadGuid} は1機ずつ運ぶため countPerLaunch が1である必要があります");
                // 起動時はスタックを生成せず、表の数量から必要容量を求める
                // At boot, calculate capacity from table counts without allocating stacks
                var counts = payload.ArrivalOutputs.Select(output => (MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                var neededSlots = MasterHolder.ItemMaster.CountRequiredSlotsAtLevelOne(counts);
                if (neededSlots <= outputSlotCount) return;
                Fail($"宇宙エレベーター拠点 {blockName} の積荷 {payloadGuid} 1機の成果は出口 {neededSlots} スロットぶんあり、outputSlotCount {outputSlotCount} に入りません");
            }

            void Fail(string reason)
            {
                Debug.LogError(reason);
                throw new InvalidOperationException(reason);
            }

            #endregion
        }

    }
}
