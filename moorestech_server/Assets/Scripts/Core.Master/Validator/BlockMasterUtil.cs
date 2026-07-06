using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;

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
            errorLogs += GearConsumptionValidation();
            errorLogs += BlockDestructionCategoryValidation();
            errorLogs += ConnectorSettingsValidation();
            errorLogs += ConnectorShapeGuidValidation();
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

                    // ElectricToGearGenerator: outputModes
                    // ElectricToGearGenerator: outputModes
                    if (block.BlockParam is ElectricToGearGeneratorBlockParam electricToGear)
                    {
                        // teethCount はギア比計算 (connectGear.TeethCount / gear.TeethCount) の除数になるため 0/負値を弾く
                        // teethCount is a divisor in the gear-ratio calc, so reject 0/negative to avoid Infinity/NaN RPM
                        if (electricToGear.TeethCount <= 0)
                        {
                            logs += $"[BlockMaster] Name:{block.Name} teethCount must be > 0 (got {electricToGear.TeethCount})\n";
                        }
                        if (electricToGear.OutputModes == null || electricToGear.OutputModes.Length == 0)
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has empty outputModes\n";
                        }
                        else
                        {
                            foreach (var mode in electricToGear.OutputModes)
                            {
                                if (mode.RequiredPower <= 0)
                                    logs += $"[BlockMaster] Name:{block.Name} outputMode requiredPower must be > 0 (got {mode.RequiredPower})\n";
                                if (mode.Rpm < 0 || mode.Torque < 0)
                                    logs += $"[BlockMaster] Name:{block.Name} outputMode rpm/torque must be >= 0\n";
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

            string GearConsumptionValidation()
            {
                // 全BlockParamのGearConsumptionを検証する
                // Validate GearConsumption on every block param that has one
                var logs = "";
                foreach (var block in blocks.Data)
                {
                    var consumption = ExtractGearConsumption(block.BlockParam);
                    if (consumption == null) continue;
                    if (!ValidateGearConsumption(consumption, out var error)) continue;
                    logs += $"[BlockMaster] Name:{block.Name} has invalid GearConsumption: {error}\n";
                }
                return logs;

                bool ValidateGearConsumption(GearConsumption c, out string error)
                {
                    if (c.BaseRpm <= 0)
                    {
                        error = $"baseRpm must be > 0 (got {c.BaseRpm})";
                        return true;
                    }
                    if (c.MinimumRpm < 0)
                    {
                        error = $"minimumRpm must be >= 0 (got {c.MinimumRpm})";
                        return true;
                    }
                    if (c.MinimumRpm > c.BaseRpm)
                    {
                        error = $"minimumRpm ({c.MinimumRpm}) must be <= baseRpm ({c.BaseRpm})";
                        return true;
                    }
                    error = null;
                    return false;
                }
            }

            string BlockDestructionCategoryValidation()
            {
                // 破壊カテゴリ定義のblockGuid実在性と、複数カテゴリへの重複登録を検証する
                // Validate blockGuid existence and reject a blockGuid registered under more than one category
                var logs = "";
                var assignedCategoryByBlockGuid = new Dictionary<Guid, string>();
                foreach (var category in blocks.BlockDestructionCategories)
                foreach (var target in category.TargetBlocks)
                {
                    // foreignKeyは自動生成されないため参照先の実在を手動で確認する
                    // foreignKey validation is not auto-generated, so verify the referenced block exists
                    if (!ExistsBlockGuid(target.BlockGuid))
                    {
                        logs += $"[BlockMaster] DestructionCategory:{category.CategoryKey} has invalid BlockGuid:{target.BlockGuid}\n";
                    }

                    // 逆引きは1ブロック1カテゴリ前提。重複するとロード順で結果が変わるため弾く
                    // The reverse lookup assumes one category per block; duplicates make the result order-dependent
                    if (assignedCategoryByBlockGuid.TryGetValue(target.BlockGuid, out var existingCategory))
                    {
                        logs += $"[BlockMaster] BlockGuid:{target.BlockGuid} is assigned to multiple destruction categories ({existingCategory}, {category.CategoryKey})\n";
                    }
                    else
                    {
                        assignedCategoryByBlockGuid.Add(target.BlockGuid, category.CategoryKey);
                    }
                }

                return logs;
            }

            string ConnectorSettingsValidation()
            {
                // 互換ペアの参照先形状の実在を検証（foreignKeyは自動生成されないため手動確認）
                // Validate pair references exist (foreignKey validation is not auto-generated)
                var logs = "";
                var connectableShapePairs = blocks.ConnectorSettings?.ConnectableShapePairs;
                if (connectableShapePairs == null) return logs;
                foreach (var pair in connectableShapePairs)
                {
                    if (!ExistsConnectorShape(pair.Shape0)) logs += $"[BlockMaster] ConnectableShapePair has invalid Shape0:{pair.Shape0}\n";
                    if (!ExistsConnectorShape(pair.Shape1)) logs += $"[BlockMaster] ConnectableShapePair has invalid Shape1:{pair.Shape1}\n";
                }
                return logs;
            }

            string ConnectorShapeGuidValidation()
            {
                // コネクタに設定されたshapeGuidの実在を検証（fluid側は形状運用開始時に追加する）
                // Validate shapeGuid on connectors (fluid-side check to be added when fluids adopt shapes)
                var logs = "";
                foreach (var block in blocks.Data)
                {
                    if (block.BlockParam is IGearConnectors gearConnectors)
                        foreach (var connector in gearConnectors.Gear.GearConnects)
                            logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);

                    if (block.BlockParam is IInventoryConnectors inventoryConnectors)
                    {
                        var connects = inventoryConnectors.InventoryConnectors;
                        if (connects.InputConnects != null)
                            foreach (var connector in connects.InputConnects)
                                logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);
                        if (connects.OutputConnects != null)
                            foreach (var connector in connects.OutputConnects)
                                logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);
                    }
                }
                return logs;

                string ValidateConnectorShapeGuid(string blockName, Guid? shapeGuid)
                {
                    if (shapeGuid == null || ExistsConnectorShape(shapeGuid.Value)) return "";
                    return $"[BlockMaster] Name:{blockName} has invalid connector ShapeGuid:{shapeGuid}\n";
                }
            }

            bool ExistsConnectorShape(Guid shapeGuid)
            {
                var connectorShapes = blocks.ConnectorSettings?.ConnectorShapes;
                return connectorShapes != null && Array.Exists(connectorShapes, s => s.ShapeGuid == shapeGuid);
            }

            bool ExistsBlockGuid(Guid blockGuid)
            {
                return Array.Exists(blocks.Data, b => b.BlockGuid == blockGuid);
            }

            GearConsumption ExtractGearConsumption(object blockParam)
            {
                // gearConsumptionを持つ全BlockParam型を列挙して取り出す
                // Enumerate every BlockParam type that carries a gearConsumption and return it
                return blockParam switch
                {
                    GearBlockParam gear => gear.GearConsumption,
                    ShaftBlockParam shaft => shaft.GearConsumption,
                    GearChainPoleBlockParam chainPole => chainPole.GearConsumption,
                    GearMachineBlockParam machine => machine.GearConsumption,
                    GearBeltConveyorBlockParam belt => belt.GearConsumption,
                    GearMinerBlockParam miner => miner.GearConsumption,
                    GearMapObjectMinerBlockParam mapMiner => mapMiner.GearConsumption,
                    GearPumpBlockParam pump => pump.GearConsumption,
                    GearToElectricGeneratorBlockParam electric => electric.GearConsumption,
                    _ => null,
                };
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
