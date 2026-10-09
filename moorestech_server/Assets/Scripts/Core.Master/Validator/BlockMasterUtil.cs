using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;
using Core.Master.Validator.Block;

namespace Core.Master.Validator
{
    public static class BlockMasterUtil
    {
        public static bool Validate(Blocks blocks, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += BlockParamValidator.Validate(blocks);
            errorLogs += BlockConstructionValidator.ValidateRequiredItems(blocks);
            errorLogs += BlockConstructionValidator.ValidatePlacementsPerCost(blocks);
            errorLogs += BlockGearConsumptionValidator.Validate(blocks);
            errorLogs += BlockDestructionCategoryValidator.Validate(blocks);
            errorLogs += BlockCategoryReferenceValidation();
            errorLogs += BlockConnectorValidator.ValidateSettings(blocks);
            errorLogs += BlockConnectorValidator.ValidateShapeGuids(blocks);
            errorLogs += BlockConnectorValidator.ValidateMeshingAxes(blocks);
            errorLogs += ExtractionSettingsValidator.Validate(blocks);
            errorLogs += BeltConveyorFamilyValidator.Validate(blocks);
            errorLogs += BeltSpeedPerTickValidator.Validate(blocks);
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string BlockCategoryReferenceValidation()
            {
                // カテゴリペアの定義有無を検証
                // Validate the category pair is defined
                var logs = string.Empty;
                foreach (var block in blocks.Data)
                {
                    if (!MasterHolder.BuildMenuCategoryMaster.Contains(block.Category, block.SubCategory))
                        logs += $"[BlockMaster] Block:{block.Name} has undefined category pair. category:{block.Category} subCategory:{block.SubCategory}\n";
                }
                return logs;
            }

            #endregion
        }

        public static void Initialize(
            Blocks blocks,
            out Dictionary<BlockId, BlockMasterElement> blockElementTableById,
            out Dictionary<Guid, BlockId> blockGuidToBlockId)
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
        }
    }
}
