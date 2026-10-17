using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    ///     ベルトと分岐器の対応付け。分岐器は「出力コネクタが2個以上のベルト」、相手は blockType と秒速が同じもの（ADR 0083）
    ///     Pairs belts with splitters: a splitter is a belt with two or more output connectors, matched by equal blockType and cells-per-second (ADR 0083)
    /// </summary>
    public static class BeltSplitterCounterpart
    {
        // 秒速はJSONの小数6桁で書かれるので、その丸め誤差より大きく、刻み(1/18≒0.055)より小さい許容で同値とみなす
        // Cells-per-second is written to six decimals, so equality tolerates more than that rounding yet far less than one step (1/18 ≈ 0.055)
        private const double SpeedTolerance = 1e-6;

        public static bool IsSplitter(IBlockParam param)
        {
            // 未定義の出力は搬出口なしとして0個に数える
            // Undefined outputs mean no emitting port and count as zero
            var outputs = ((IInventoryConnectors)param).InventoryConnectors.OutputConnects;
            return outputs != null && 1 < outputs.Length;
        }

        // 候補が無いマスタ(テスト用mod等)や2件以上で曖昧なマスタでは false を返しログに残す。呼び手は覆いを出さない
        // Returns false, with a log, on a master without a counterpart (test mods etc.) or with an ambiguous one; the caller then shows no cover
        public static bool TryGetSplitterBlockId(BlockId beltBlockId, out BlockId splitterBlockId)
        {
            var belt = MasterHolder.BlockMaster.GetBlockMaster(beltBlockId);
            if (IsSplitter(belt.BlockParam))
            {
                splitterBlockId = beltBlockId;
                return true;
            }
            var counterparts = FindCounterparts(belt);
            if (counterparts.Count != 1)
            {
                var speed = ((IBeltSpeedParam)belt.BlockParam).BeltSpeedPerSecond;
                Debug.LogError($"[BeltSplitterCounterpart] Belt {belt.Name} has {counterparts.Count} splitters with blockType:{belt.BlockType} beltSpeedPerSecond:{speed}; expected exactly one.");
                splitterBlockId = default;
                return false;
            }
            splitterBlockId = MasterHolder.BlockMaster.GetBlockId(counterparts[0].BlockGuid);
            return true;
        }

        private static List<BlockMasterElement> FindCounterparts(BlockMasterElement belt)
        {
            var speed = ((IBeltSpeedParam)belt.BlockParam).BeltSpeedPerSecond;
            var result = new List<BlockMasterElement>();
            foreach (var candidate in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (candidate.BlockParam is not IBeltSpeedParam candidateSpeed || !IsSplitter(candidate.BlockParam)) continue;
                if (candidate.BlockType != belt.BlockType) continue;
                if (SpeedTolerance <= Math.Abs(candidateSpeed.BeltSpeedPerSecond - speed)) continue;
                result.Add(candidate);
            }
            return result;
        }
    }
}
