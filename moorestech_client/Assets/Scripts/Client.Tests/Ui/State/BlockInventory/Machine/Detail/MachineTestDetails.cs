using System;
using System.Linq;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の状態詳細と param の試験用の組み立て
    /// Test builders for the machine's state details and param
    /// </summary>
    public static class MachineTestDetails
    {
        public static readonly Guid RecipeA = new("00000000-0000-0000-0000-00000000a00a");
        public static readonly Guid RecipeB = new("00000000-0000-0000-0000-00000000b00b");
        public static readonly BlockInstanceId DefaultInstanceId = new(1);

        // 区画の値の補助はマスタ読み込み前提
        // The helpers below require the master to be loaded; summaries look up the machine's blockGuid
        public static BlockMasterElement MachineBlock => MasterHolder.BlockMaster.Blocks.Data.First(b => b.BlockParam is IMachineParam);

        // 各 GUID を出力 1 個のレシピとして解放した機械
        // A machine with each GUID unlocked as a 1-item recipe
        public static MachineSectionViewModel ViewModel(MachineRecipeSelection selection, params Guid[] unlockedRecipeGuids)
        {
            return ViewModelFor(DefaultInstanceId, Detail(selection, Unbound(new MachineSlotLayout(1, 1, 0)), 0), 2, Catalog(unlockedRecipeGuids));
        }

        // モジュール付き未選択・レシピ 0 件の機械
        // An unselected machine with modules and no recipes, for slot gestures in inventory mode
        public static MachineSectionViewModel ViewModelWithModules(int moduleSlotCount, float progress, int inputCount)
        {
            var detail = Detail(MachineRecipeSelection.Unselected, Unbound(new MachineSlotLayout(inputCount, 1, moduleSlotCount)), progress);
            return ViewModelFor(DefaultInstanceId, detail, inputCount + 1 + moduleSlotCount, MachineRecipeCatalog.Empty);
        }

        public static MachineSectionViewModel ViewModelFor(BlockInstanceId instanceId, MachineDetail detail, int itemSlotCount, MachineRecipeCatalog catalog)
        {
            var slots = new SlotItem[itemSlotCount];
            for (var i = 0; i < slots.Length; i++) slots[i] = new SlotItem(new ItemId(0), 0);
            return MachineSectionViewModelBuilder.Build(Panel(instanceId, slots), detail, slots, Array.Empty<BlockFluidSlotState>(), catalog, 1);
        }

        // InternalsVisibleTo で直に組む
        // Builds a machine panel via InternalsVisibleTo
        public static OpenBlockInventoryPanel Panel(BlockInstanceId instanceId, SlotItem[] slots)
        {
            var block = MachineBlock;
            return new OpenBlockInventoryPanel(block.BlockType, block.BlockGuid, MasterHolder.BlockMaster.GetBlockId(block.BlockGuid), instanceId, new Vector3Int(1, 0, 0), slots, BlockInventoryPanelError.None);
        }

        public static MachineRecipeCatalog Catalog(params Guid[] unlockedRecipeGuids)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(MachineBlock.BlockGuid);
            var recipes = unlockedRecipeGuids.Select(guid => new MachineRecipeData(guid, blockId, 2, Array.Empty<ItemAmount>(), new[] { new ItemAmount(new ItemId(1), 1) }, Array.Empty<RecipeFluidAmount>(), Array.Empty<RecipeFluidAmount>(), RecipeLevelInheritance.None)).ToArray();
            return MachineRecipeCatalog.Build(recipes);
        }

        public static MachineDetail Detail(MachineRecipeSelection selection, MachineBindingDetail binding, float progress)
        {
            return new MachineDetail(selection, MachineProcessDisplayState.Processing, 1, 1, progress, false, false, 2, Array.Empty<RecipeOutputItem>(), binding);
        }

        public static MachineBindingDetail Unbound(MachineSlotLayout layout)
        {
            return new MachineBindingDetail(layout, Array.Empty<MachineItemSlotBinding>(), Array.Empty<MachineTankBinding>());
        }

        public static CommonMachineBlockStateDetail Common(string stateType, float currentPower, float requestPower)
        {
            return new CommonMachineBlockStateDetail(currentPower, requestPower, 0.25f, stateType, VanillaMachineBlockStateConst.IdleState);
        }

        public static MachineBlockStateDetail Machine(Guid runningRecipe, Guid selectedRecipe, bool outputSpaceShort)
        {
            return new MachineBlockStateDetail(0.5f, runningRecipe, selectedRecipe, outputSpaceShort);
        }

        // 構成の数だけを持つ IMachineParam の偽物
        // A fake IMachineParam that only carries the layout counts
        public sealed class FakeMachineParam : IMachineParam
        {
            public int InputSlotCount { get; }
            public int OutputSlotCount { get; }
            public int InputTankCount { get; }
            public int OutputTankCount { get; }
            public float InnerTankCapacity => 0;
            public int ModuleSlotCount { get; }
            public bool AllowQualityModule => false;
            public FluidInventoryConnects FluidInventoryConnectors => null;

            public FakeMachineParam(int inputSlotCount, int outputSlotCount, int inputTankCount, int outputTankCount, int moduleSlotCount)
            {
                InputSlotCount = inputSlotCount;
                OutputSlotCount = outputSlotCount;
                InputTankCount = inputTankCount;
                OutputTankCount = outputTankCount;
                ModuleSlotCount = moduleSlotCount;
            }
        }
    }
}
