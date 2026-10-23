using System;
using System.Collections.Generic;
using Core.Item.Interface;
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
            // 1機の問い合わせは、countPerLaunch が1でない積荷を Preview 自身の整数倍検査で例外にする
            // Previewing a single satellite throws via Preview's own multiple check when countPerLaunch is not one
            void ThrowIfResultNeverFits(string blockName, Guid payloadGuid, int outputSlotCount)
            {
                var outputs = _orbitalArrivalService.PreviewArrivalOutputs(MasterHolder.ItemMaster.GetItemId(payloadGuid), 1);
                var neededSlots = CountSlotsAtStackLevelOne(outputs);
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

        // 成果をアイテムごとに合算し、スタック上限が最も低いレベル1で何スロット要るかを数える
        // Sum the results per item and count the slots needed at stack level 1, the lowest stack limit
        // スタック上限は研究で上がるだけなので、いちばん小さいレベル1で入れば実行時の今の上限でも必ず入る
        // Stack limits only rise with research, so fitting at level one guarantees a fit at any runtime level
        private static int CountSlotsAtStackLevelOne(IReadOnlyList<IItemStack> outputs)
        {
            var totals = new Dictionary<ItemId, int>();
            foreach (var output in outputs) totals[output.Id] = totals.GetValueOrDefault(output.Id) + output.Count;

            var slots = 0;
            foreach (var pair in totals)
            {
                var element = MasterHolder.ItemMaster.GetItemMaster(pair.Key);
                var stackAtLevelOne = MasterHolder.ItemMaster.GetStackLevelTable(element.StackLevelTableGuid).StackCounts[0];
                slots += (pair.Value + stackAtLevelOne - 1) / stackAtLevelOne;
            }
            return slots;
        }
    }
}
