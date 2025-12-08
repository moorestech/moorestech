using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BlocksModule;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using UnitGenerator;

namespace Core.Master
{
    // アイテムId専用の方を定義
    // NOTE このIDは永続化されれることはなく、メモリ上、ネットワーク通信上でのみ使用する値
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public partial struct BlockId
    {
    }
    
    public class BlockMaster
    {
        public readonly Blocks Blocks;
        
        private readonly Dictionary<BlockId, BlockMasterElement> _blockElementTableById;
        private readonly Dictionary<Guid, BlockId> _blockGuidToBlockId;
        
        private readonly Dictionary<ItemId, BlockId> _itemIdToBlockId;
        
        public BlockMaster(JToken blockJToken, ItemMaster itemMaster)
        {
            // GUIDの順番にint型のItemIdを割り当てる
            Blocks = BlocksLoader.Load(blockJToken);
            var sortedBlockElements = Blocks.Data.ToList().OrderBy(x => x.BlockGuid).ToList();
            
            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            _blockElementTableById = new Dictionary<BlockId, BlockMasterElement>();
            _blockGuidToBlockId = new Dictionary<Guid, BlockId>();
            for (var i = 0; i < sortedBlockElements.Count; i++)
            {
                var blockId = new BlockId(i + 1); // アイテムID 0は空のアイテムとして予約しているので、1から始める
                _blockElementTableById.Add(blockId, sortedBlockElements[i]);
                _blockGuidToBlockId.Add(sortedBlockElements[i].BlockGuid, blockId);
            }
            
            // itemId to blockId
            _itemIdToBlockId = new Dictionary<ItemId, BlockId>();
            foreach (var blockElement in Blocks.Data)
            {
                var itemId = itemMaster.GetItemIdOrNull(blockElement.ItemGuid);
                if (itemId == null)
                {
                    throw new InvalidOperationException($"ItemElement not found. BlockName:{blockElement.Name} ItemGuid:{blockElement.ItemGuid}");
                }
                if (_itemIdToBlockId.TryGetValue(itemId.Value, out var blockId))
                {
                    var existingBlockElement = GetBlockMaster(blockId);
                    throw new InvalidOperationException($"Duplicate itemId. Name1: {blockElement.Name}  Name2: {existingBlockElement.Name} ItemId:{blockElement.ItemGuid} BlockId:{blockElement.BlockGuid}");
                }
                
                _itemIdToBlockId.Add(itemId.Value, _blockGuidToBlockId[blockElement.BlockGuid]);
            }

            // 外部キーバリデーション
            // Foreign key validation
            BlockParamValidation();
            OverrideVerticalBlockValidation();
            GearChainItemsValidation();

            #region Internal

            void BlockParamValidation()
            {
                var errorLogs = "";
                foreach (var block in Blocks.Data)
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
                                    errorLogs += $"[BlockMaster] Name:{block.Name} has invalid FuelItem.ItemGuid:{fuelItem.ItemGuid}\n";
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
                                    errorLogs += $"[BlockMaster] Name:{block.Name} has invalid FuelFluid.FluidGuid:{fuelFluid.FluidGuid}\n";
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
                                    errorLogs += $"[BlockMaster] Name:{block.Name} has invalid GearFuelItem.ItemGuid:{gearFuelItem.ItemGuid}\n";
                                }
                            }
                        }
                        foreach (var requiredFluid in fuelGearGenerator.RequiredFluids)
                        {
                            var id = MasterHolder.FluidMaster.GetFluidIdOrNull(requiredFluid.FluidGuid);
                            if (id == null)
                            {
                                errorLogs += $"[BlockMaster] Name:{block.Name} has invalid RequiredFluid.FluidGuid:{requiredFluid.FluidGuid}\n";
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
                                errorLogs += $"[BlockMaster] Name:{block.Name} has invalid RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                            }
                        }
                        // 空のGUIDはアップグレードなしを意味するためスキップ
                        // Empty GUID means no upgrade, so skip
                        if (baseCamp.UpgradBlockGuid != Guid.Empty)
                        {
                            var upgradeBlockId = GetBlockIdOrNull(baseCamp.UpgradBlockGuid);
                            if (upgradeBlockId == null)
                            {
                                errorLogs += $"[BlockMaster] Name:{block.Name} has invalid UpgradBlockGuid:{baseCamp.UpgradBlockGuid}\n";
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void OverrideVerticalBlockValidation()
            {
                var errorLogs = "";
                foreach (var block in Blocks.Data)
                {
                    if (block.OverrideVerticalBlock == null) continue;

                    var overrideVertical = block.OverrideVerticalBlock;

                    // 空のGUIDは「オーバーライドなし」を意味するためスキップ
                    // Empty GUID means no override, so skip
                    if (overrideVertical.UpBlockGuid.HasValue && overrideVertical.UpBlockGuid.Value != Guid.Empty)
                    {
                        var id = GetBlockIdOrNull(overrideVertical.UpBlockGuid.Value);
                        if (id == null)
                        {
                            errorLogs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.UpBlockGuid:{overrideVertical.UpBlockGuid}\n";
                        }
                    }
                    if (overrideVertical.HorizontalBlockGuid.HasValue && overrideVertical.HorizontalBlockGuid.Value != Guid.Empty)
                    {
                        var id = GetBlockIdOrNull(overrideVertical.HorizontalBlockGuid.Value);
                        if (id == null)
                        {
                            errorLogs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.HorizontalBlockGuid:{overrideVertical.HorizontalBlockGuid}\n";
                        }
                    }
                    if (overrideVertical.DownBlockGuid.HasValue && overrideVertical.DownBlockGuid.Value != Guid.Empty)
                    {
                        var id = GetBlockIdOrNull(overrideVertical.DownBlockGuid.Value);
                        if (id == null)
                        {
                            errorLogs += $"[BlockMaster] Name:{block.Name} has invalid OverrideVerticalBlock.DownBlockGuid:{overrideVertical.DownBlockGuid}\n";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            void GearChainItemsValidation()
            {
                var errorLogs = "";
                foreach (var gearChainItem in Blocks.GearChainItems)
                {
                    var id = MasterHolder.ItemMaster.GetItemIdOrNull(gearChainItem.ItemGuid);
                    if (id == null)
                    {
                        errorLogs += $"[BlockMaster] GearChainItem has invalid ItemGuid:{gearChainItem.ItemGuid}\n";
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            #endregion
        }
        
        public BlockMasterElement GetBlockMaster(BlockId blockId)
        {
            if (!_blockElementTableById.TryGetValue(blockId, out var element))
            {
                throw new InvalidOperationException($"BlockElement not found. BlockId:{blockId}");
            }
            
            return element;
        }
        
        public BlockMasterElement GetBlockMaster(Guid blockGuid)
        {
            var blockId = GetBlockId(blockGuid);
            return GetBlockMaster(blockId);
        }
        
        public BlockMasterElement GetBlockMaster(ItemId itemId)
        {
            var blockId = GetBlockId(itemId);
            return GetBlockMaster(blockId);
        }
        
        public BlockId GetBlockId(Guid blockGuid)
        {
            if (!_blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                throw new InvalidOperationException($"BlockElement not found. BlockGuid:{blockGuid}");
            }

            return blockId;
        }

        public BlockId? GetBlockIdOrNull(Guid blockGuid)
        {
            if (!_blockGuidToBlockId.TryGetValue(blockGuid, out var blockId))
            {
                return null;
            }

            return blockId;
        }
        
        public List<BlockId> GetBlockAllIds()
        {
            return _blockElementTableById.Keys.ToList();
        }
        
        public bool IsBlock(ItemId itemId)
        {
            return _itemIdToBlockId.ContainsKey(itemId);
        }
        
        public BlockId GetBlockId(ItemId itemId)
        {
            return _itemIdToBlockId[itemId];
        }
        
        public ItemId GetItemId(BlockId blockId)
        {
            foreach (var pair in _itemIdToBlockId)
            {
                if (pair.Value == blockId)
                {
                    return pair.Key;
                }
            }
            
            throw new InvalidOperationException($"ItemElement not found. BlockId:{blockId}");
        }
    }
}