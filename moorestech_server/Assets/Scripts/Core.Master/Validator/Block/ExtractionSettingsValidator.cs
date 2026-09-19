using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    ///     採取設定の参照整合性・正値を検証する
    ///     Validates that extraction settings satisfy reference integrity and positive values
    /// </summary>
    internal static class ExtractionSettingsValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is IMinerParam minerParam) logs += ValidateMineSettings(block.Name, minerParam);
                if (block.BlockParam is GearMapObjectMinerBlockParam mapObjectMinerParam) logs += ValidateMapObjectMineSettings(block.Name, mapObjectMinerParam);
                if (block.BlockParam is IPumpParam pumpParam) logs += ValidateGenerateFluids(block.Name, pumpParam);
            }
            return logs;
        }

        private static string ValidateMineSettings(string blockName, IMinerParam minerParam)
        {
            var logs = "";
            var uniqueItemGuids = new HashSet<Guid>();
            foreach (var miningSetting in minerParam.MineSettings.items)
            {
                // 削除済みアイテムを指すと、その採掘機を持った瞬間やロード時に実行時例外になるため起動時に弾く
                // A deleted item would throw at runtime when the miner is held or loaded, so reject it at startup
                if (MasterHolder.ItemMaster.GetItemIdOrNull(miningSetting.ItemGuid) == null)
                    logs += $"[BlockMaster] Name:{blockName} has invalid MineSettings.ItemGuid:{miningSetting.ItemGuid}\n";

                // 0秒以下・NaN・Infinityの採掘時間はtick換算で0になり毎tick産出する
                // A mining time that is zero, negative, NaN or Infinity converts to zero ticks and yields every tick
                if (!(0 < miningSetting.Time) || float.IsInfinity(miningSetting.Time))
                    logs += $"[BlockMaster] Name:{blockName} has non-positive or non-finite MineSettings.Time:{miningSetting.Time} for ItemGuid:{miningSetting.ItemGuid}\n";

                uniqueItemGuids.Add(miningSetting.ItemGuid);
            }

            // 採掘機は跨いだ鉱脈のアイテムを1種1スロットで同時に出す。枠が足りないとInsertionCheckが通らず永久Idleになる
            // A miner outputs one slot per straddled vein item at once; too few slots fail InsertionCheck and leave it idle forever
            if (minerParam.OutputItemSlotCount < uniqueItemGuids.Count)
                logs += $"[BlockMaster] Name:{blockName} has outputItemSlotCount:{minerParam.OutputItemSlotCount} smaller than the {uniqueItemGuids.Count} unique mineSettings items\n";
            return logs;
        }

        private static string ValidateMapObjectMineSettings(string blockName, GearMapObjectMinerBlockParam mapObjectMinerParam)
        {
            var logs = "";
            foreach (var mineSetting in mapObjectMinerParam.MapObjectMineSettings.items)
            {
                // foreignKeyは自動検証されないため、欠損したmapObjectは無言で読み飛ばさず検証エラーにする
                // foreignKeys are not auto-validated, so a missing map object is a validation error instead of a silent skip
                var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(mineSetting.MapObjectGuid);
                if (mapObjectElement == null)
                {
                    logs += $"[BlockMaster] Name:{blockName} has invalid MapObjectMineSettings.MapObjectGuid:{mineSetting.MapObjectGuid}\n";
                    continue;
                }

                // 装飾物は削れないので、採掘機が対象に載せても永久に何も採れない誤設定になる
                // A decoration can never be worn down, so listing one as a miner target mines nothing forever
                if (MapObjectMaster.IsDecoration(mapObjectElement))
                    logs += $"[BlockMaster] Name:{blockName} points MapObjectMineSettings.MapObjectGuid:{mineSetting.MapObjectGuid} which forbids mining\n";
            }
            return logs;
        }

        // 生成時間・生成量が0以下・NaN・Infinityだと毎秒生成量が無限大や0、NaNになるため、実行時の読み飛ばしでなく起動時に弾く
        // Non-positive, NaN or Infinity generate time or amount makes the per-second rate infinite, zero or NaN, so reject it at startup instead of skipping at runtime
        private static string ValidateGenerateFluids(string blockName, IPumpParam pumpParam)
        {
            var logs = "";
            // 内部タンクは単一流体で先頭行だけを採用するため、同一流体の重複行は表現を許さない
            // The inner tank is single-fluid and only the first row is used, so duplicate rows for one fluid are not allowed
            var declaredFluidGuids = new HashSet<Guid>();
            foreach (var generateFluid in pumpParam.GenerateFluid.items)
            {
                if (MasterHolder.FluidMaster.GetFluidIdOrNull(generateFluid.FluidGuid) == null)
                    logs += $"[BlockMaster] Name:{blockName} has invalid GenerateFluid.FluidGuid:{generateFluid.FluidGuid}\n";
                if (!declaredFluidGuids.Add(generateFluid.FluidGuid))
                    logs += $"[BlockMaster] Name:{blockName} has duplicated GenerateFluid.FluidGuid:{generateFluid.FluidGuid}\n";

                if (!(0 < generateFluid.GenerateTime) || float.IsInfinity(generateFluid.GenerateTime))
                    logs += $"[BlockMaster] Name:{blockName} has non-positive or non-finite GenerateFluid.GenerateTime:{generateFluid.GenerateTime} for FluidGuid:{generateFluid.FluidGuid}\n";
                if (!(0 < generateFluid.Amount) || float.IsInfinity(generateFluid.Amount))
                    logs += $"[BlockMaster] Name:{blockName} has non-positive or non-finite GenerateFluid.Amount:{generateFluid.Amount} for FluidGuid:{generateFluid.FluidGuid}\n";

                // 極小のGenerateTimeで割ると毎秒生成量が桁あふれし、後続のタンク量・搬出計算が壊れる
                // Dividing by a tiny GenerateTime can overflow the per-second rate, corrupting downstream tank/output math
                if (float.IsInfinity(generateFluid.Amount / generateFluid.GenerateTime))
                    logs += $"[BlockMaster] Name:{blockName} has an overflowing GenerateFluid rate Amount:{generateFluid.Amount} / GenerateTime:{generateFluid.GenerateTime} for FluidGuid:{generateFluid.FluidGuid}\n";
            }
            return logs;
        }
    }
}
