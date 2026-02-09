using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    public static class BlockMasterUtil
    {
        public static bool Validate(Blocks blocks, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += BlockItemGuidValidation();
            errorLogs += BlockParamValidation();
            errorLogs += OverrideVerticalBlockValidation();
            errorLogs += GearChainItemsValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string BlockItemGuidValidation()
            {
                var logs = "";
                foreach (var blockElement in blocks.Data)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(blockElement.ItemGuid);
                    if (itemId == null)
                    {
                        logs += $"[BlockMaster] Name:{blockElement.Name} has invalid ItemGuid:{blockElement.ItemGuid}\n";
                    }
                }

                return logs;
            }

            string BlockParamValidation()
            {
                var logs = "";
                foreach (var block in blocks.Data)
                {
                    // ElectricGenerator: fuelItems, fuelFluids
                    // ElectricGenerator: fuelItems, fuelFluids
                    if (block.BlockParam is ElectricGeneratorBlockParam electricGenerator)
                    {
                        if (electricGenerator.FuelItems != null)
                        {
                            foreach (var fuelItem in electricGenerator.FuelItems)
                            {
                                var id = MasterHolder.ItemMaster.GetItemIdOrNull(fuelItem.ItemGuid);
                                if (id == null)
                                {
                                    logs += $"[BlockMaster] Name:{block.Name} has invalid FuelItem.ItemGuid:{fuelItem.ItemGuid}\n";
                                }
                            }
                        }
                        if (electricGenerator.FuelFluids != null)
                        {
                            foreach (var fuelFluid in electricGenerator.FuelFluids)
                            {
                                var id = MasterHolder.FluidMaster.GetFluidIdOrNull(fuelFluid.FluidGuid);
                                if (id == null)
                                {
                                    logs += $"[BlockMaster] Name:{block.Name} has invalid FuelFluid.FluidGuid:{fuelFluid.FluidGuid}\n";
                                }
                            }
                        }
                    }

                    // FuelGearGenerator: gearFuelItems, requiredFluids
                    // FuelGearGenerator: gearFuelItems, requiredFluids
                    if (block.BlockParam is FuelGearGeneratorBlockParam fuelGearGenerator)
                    {
                        if (fuelGearGenerator.GearFuelItems != null)
                        {
                            foreach (var gearFuelItem in fuelGearGenerator.GearFuelItems)
                            {
                                var id = MasterHolder.ItemMaster.GetItemIdOrNull(gearFuelItem.ItemGuid);
                                if (id == null)
                                {
                                    logs += $"[BlockMaster] Name:{block.Name} has invalid GearFuelItem.ItemGuid:{gearFuelItem.ItemGuid}\n";
                                }
                            }
                        }
                        foreach (var requiredFluid in fuelGearGenerator.RequiredFluids)
                        {
                            var id = MasterHolder.FluidMaster.GetFluidIdOrNull(requiredFluid.FluidGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredFluid.FluidGuid:{requiredFluid.FluidGuid}\n";
                            }
                        }
                    }

                    // BaseCamp: requiredItems, upgradBlockGuid
                    // BaseCamp: requiredItems, upgradBlockGuid
                    if (block.BlockParam is BaseCampBlockParam baseCamp)
                    {
                        foreach (var requiredItem in baseCamp.RequiredItems)
                        {
                            var id = MasterHolder.ItemMaster.GetItemIdOrNull(requiredItem.ItemGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                            }
                        }
                        // 空のGUIDはアップグレードなしを意味するためスキップ
                        // Empty GUID means no upgrade, so skip
                        if (baseCamp.UpgradBlockGuid != Guid.Empty)
                        {
                            if (!ExistsBlockGuid(baseCamp.UpgradBlockGuid))
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid UpgradBlockGuid:{baseCamp.UpgradBlockGuid}\n";
                            }
                        }
                    }

                    // GearPump: generateFluid
                    // GearPump: generateFluid
                    if (block.BlockParam is GearPumpBlockParam gearPump)
                    {
                        foreach (var generateFluid in gearPump.GenerateFluid.items)
                        {
                            var id = MasterHolder.FluidMaster.GetFluidIdOrNull(generateFluid.FluidGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid GenerateFluid.FluidGuid:{generateFluid.FluidGuid}\n";
                            }
                        }
                    }

                    // ElectricPump: generateFluid
                    // ElectricPump: generateFluid
                    if (block.BlockParam is ElectricPumpBlockParam electricPump)
                    {
                        foreach (var generateFluid in electricPump.GenerateFluid.items)
                        {
                            var id = MasterHolder.FluidMaster.GetFluidIdOrNull(generateFluid.FluidGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid GenerateFluid.FluidGuid:{generateFluid.FluidGuid}\n";
                            }
                        }
                    }
                }

                return logs;
            }

            string OverrideVerticalBlockValidation()
            {
                var logs = "";
                foreach (var block in blocks.Data)
                {
                    if (block.OverrideVerticalBlock == null) continue;

                    var overrideVertical = block.OverrideVerticalBlock;

                    // 空のGUIDは「オーバーライドなし」を意味するためスキップ
                    // Empty GUID means no override, so skip
                    if (overrideVertical.UpBlockGuid.HasValue && overrideVertical.UpBlockGuid.Value != Guid.Empty)
                    {
                        if (!ExistsBlockGuid(overrideVertical.UpBlockGuid.Value))
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.UpBlockGuid:{overrideVertical.UpBlockGuid}\n";
                        }
                    }
                    if (overrideVertical.HorizontalBlockGuid.HasValue && overrideVertical.HorizontalBlockGuid.Value != Guid.Empty)
                    {
                        if (!ExistsBlockGuid(overrideVertical.HorizontalBlockGuid.Value))
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.HorizontalBlockGuid:{overrideVertical.HorizontalBlockGuid}\n";
                        }
                    }
                    if (overrideVertical.DownBlockGuid.HasValue && overrideVertical.DownBlockGuid.Value != Guid.Empty)
                    {
                        if (!ExistsBlockGuid(overrideVertical.DownBlockGuid.Value))
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.DownBlockGuid:{overrideVertical.DownBlockGuid}\n";
                        }
                    }
                }

                return logs;
            }

            string GearChainItemsValidation()
            {
                var logs = "";
                foreach (var gearChainItem in blocks.GearChainItems)
                {
                    var id = MasterHolder.ItemMaster.GetItemIdOrNull(gearChainItem.ItemGuid);
                    if (id == null)
                    {
                        logs += $"[BlockMaster] GearChainItem has invalid ItemGuid:{gearChainItem.ItemGuid}\n";
                    }
                }

                return logs;
            }

            bool ExistsBlockGuid(Guid blockGuid)
            {
                return Array.Exists(blocks.Data, b => b.BlockGuid == blockGuid);
            }

            #endregion
        }

        public static void Initialize(
            Blocks blocks,
            out Dictionary<BlockId, BlockMasterElement> blockElementTableById,
            out Dictionary<Guid, BlockId> blockGuidToBlockId,
            out Dictionary<ItemId, BlockId> itemIdToBlockId,
            Func<BlockId, BlockMasterElement> getBlockMaster)
        {
            // GUIDの順番にint型のBlockIdを割り当てる
            // Assign int BlockId in order of GUID
            var sortedBlockElements = blocks.Data.ToList().OrderBy(x => x.BlockGuid).ToList();

            // ブロックID 0は空のブロックとして予約しているので、1から始める
            // Block ID 0 is reserved for empty block, so start from 1
            blockElementTableById = new Dictionary<BlockId, BlockMasterElement>();
            blockGuidToBlockId = new Dictionary<Guid, BlockId>();
            for (var i = 0; i < sortedBlockElements.Count; i++)
            {
                var blockId = new BlockId(i + 1);
                blockElementTableById.Add(blockId, sortedBlockElements[i]);
                blockGuidToBlockId.Add(sortedBlockElements[i].BlockGuid, blockId);
            }

            // ItemIdからBlockIdへのマッピングを構築
            // Build mapping from ItemId to BlockId
            itemIdToBlockId = new Dictionary<ItemId, BlockId>();
            foreach (var blockElement in blocks.Data)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(blockElement.ItemGuid);
                if (itemIdToBlockId.TryGetValue(itemId, out var existingBlockId))
                {
                    var existingBlockElement = getBlockMaster(existingBlockId);
                    throw new InvalidOperationException($"Duplicate itemId. Name1: {blockElement.Name}  Name2: {existingBlockElement.Name} ItemId:{blockElement.ItemGuid} BlockId:{blockElement.BlockGuid}");
                }

                itemIdToBlockId.Add(itemId, blockGuidToBlockId[blockElement.BlockGuid]);
            }
        }
    }
}
