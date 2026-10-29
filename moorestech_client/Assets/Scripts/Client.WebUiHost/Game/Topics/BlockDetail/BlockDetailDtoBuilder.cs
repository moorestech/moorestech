using System.Collections.Generic;
using Game.Block.Interface.Component;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom;
using Client.WebUiHost.Game.Topics.BlockDetail.MachineInput;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Fluid;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using System;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// BlockGameObject の StateDetail 群とマスタ値から capability 詳細を dto へ充填する
    /// Fills capability details into the dto from the BlockGameObject's state details and master values
    /// </summary>
    public static class BlockDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache, BeltTransportClientCache beltCache, FluidNetworkClientCache fluidCache)
        {
            var param = block.BlockMasterElement.BlockParam;
            ElectricToGearDetailDtoBuilder.Apply(dto, block, param);
            SpaceElevatorDetailDtoBuilder.Apply(dto, block, param);

            // ベルト: クライアントの複製が数えた搬出統計
            // Belts: exit statistics counted by the client's replica
            BeltThroughputDetailDtoBuilder.Apply(dto, block, param, beltCache);
            GearToElectricDetailDtoBuilder.Apply(dto, block, param);
            TrainPlatformDetailDtoBuilder.Apply(dto, block, param);
            TrainStationDetailDtoBuilder.Apply(dto, block, param);
            // 部屋キャッシュと機械停止理由を反映
            // Apply the room cache and machine halt reason
            CleanRoomDetailDtoBuilder.Apply(dto, block, cache);
            // 室内機械: 選択工程の抽選の中身
            // Interior machines: draw details of the selected process
            CleanRoomYieldDetailDtoBuilder.Apply(dto, cache);

            // 機械: 継承元のレベルで止まった理由
            // Machines: the reason the inflow stalled on the inheritance source level
            MachineInputStallDetailDtoBuilder.Apply(dto, cache);
            RocketLaunchPadDetailDtoBuilder.Apply(dto, block);

            // 機械系: CommonMachine + MachineBlock の両 StateDetail が揃うブロックのみ
            // Machines: only blocks carrying both CommonMachine and MachineBlock state details
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var machineState = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (common != null && machineState != null && param is IMachineParam machineParam)
            {
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(machineState.MachineRecipeGuid));

                // 束縛は選択レシピが決めるため、加工中レシピではなく選択レシピから引く
                // The binding follows the selected recipe, so it resolves from the selection rather than the running job
                var selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(machineState.SelectedRecipeGuid));
                dto.Progress = machineState.ProcessingRate;
                dto.Machine = new MachineDetailDto
                {
                    RecipeGuid = machineState.MachineRecipeGuid,
                    SelectedRecipeGuid = machineState.SelectedRecipeGuid,
                    BlockGuid = block.BlockMasterElement.BlockGuid.ToString(),
                    RecipeTime = recipe?.Time ?? 0,
                    OutputItems = BuildMachineOutputItems(recipe),
                    CurrentState = ToCamelCase(common.CurrentStateType),
                    CurrentPower = common.CurrentPower,
                    RequestPower = common.RequestPower,
                    OutputSpaceShort = machineState.IsOutputSpaceShort,
                    SlotLayout = new SlotLayoutDto { Input = machineParam.InputSlotCount, Output = machineParam.OutputSlotCount, Module = machineParam.ModuleSlotCount, InputTank = machineParam.InputTankCount },
                    SlotBindings = MachineSlotBindingDtoBuilder.BuildSlotBindings(selectedRecipe, machineParam),
                    TankBindings = MachineSlotBindingDtoBuilder.BuildTankBindings(selectedRecipe, machineParam),
                };
            }

            // 発電機: PowerGenerator StateDetail
            // Generators: the PowerGenerator state detail
            var generator = block.GetStateDetail<PowerGeneratorStateDetail>(PowerGeneratorStateDetail.StateDetailKey);
            if (generator != null)
            {
                dto.Generator = new GeneratorDetailDto
                {
                    RemainingFuelTime = generator.RemainingFuelTime,
                    CurrentFuelTime = generator.CurrentFuelTime,
                    OperatingRate = generator.OperatingRate,
                };
            }

            // 採掘機: MinerDetailDtoBuilderが算出
            // Miners: computed by MinerDetailDtoBuilder
            MinerDetailDtoBuilder.Apply(dto, block, param, common);

            // ポンプ: PumpDetailDtoBuilderが算出
            // Pumps: computed by PumpDetailDtoBuilder
            PumpDetailDtoBuilder.Apply(dto, block, param, common);

            // ギア: 役割と現在値を算出
            // Gears: derives role and current values
            GearDetailDtoBuilder.Apply(dto, block, param);

            // 液体スロット: FluidMachineInventory StateDetail（入力→出力の順で連結）
            // Fluid slots: the FluidMachineInventory state detail (inputs then outputs)
            var fluid = block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
            if (fluid != null)
            {
                FluidSlotDtoBuilder.Append(dto.FluidSlots, fluid.InputTanks);
                FluidSlotDtoBuilder.Append(dto.FluidSlots, fluid.OutputTanks);
            }

            // パイプの中身はクライアントの流体複製から読む。複製にノードがある座標だけがパイプ
            // Pipe contents come from the client's fluid replica; only a position with a replica node is a pipe
            if (fluidCache.TryGetNodeIndex(block.BlockPosInfo.OriginalPos, out var pipeNode))
            {
                var network = fluidCache.Replica.Network;
                FluidSlotDtoBuilder.Append(dto.FluidSlots, new List<FluidMessagePack> { new(network.NodeFluidIds[pipeNode], FluidUnits.ToAmount(network.NodeAmounts[pipeNode]), FluidUnits.ToAmount(network.NodeCapacities[pipeNode])) });
            }

            ApplyNetworkCaches(dto, cache);
        }

        private static List<MachineOutputItemDto> BuildMachineOutputItems(Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe)
        {
            var result = new List<MachineOutputItemDto>();
            if (recipe == null) return result;
            foreach (var output in recipe.OutputItems)
                result.Add(new MachineOutputItemDto { ItemId = MasterHolder.ItemMaster.GetItemId(output.ItemGuid).AsPrimitive(), Count = output.Count });
            return result;
        }

        // ネットワーク集約キャッシュを dto に写す（未取得は null のままキー省略）
        // Copy network aggregate caches into the dto (unfetched stays null and is key-omitted)
        private static void ApplyNetworkCaches(BlockInventoryDto dto, BlockNetworkInfoCache cache)
        {
            if (cache.Electric != null)
            {
                dto.ElectricNetwork = new ElectricNetworkDto
                {
                    TotalGeneratePower = cache.Electric.TotalGeneratePower,
                    TotalRequiredPower = cache.Electric.TotalRequiredPower,
                    ConsumerCount = cache.Electric.ConsumerCount,
                    PowerRate = cache.Electric.PowerRate,
                };
            }
            if (cache.GearNetwork != null)
            {
                dto.GearNetwork = new GearNetworkDto
                {
                    TotalRequiredGearPower = cache.GearNetwork.TotalRequiredGearPower,
                    TotalGenerateGearPower = cache.GearNetwork.TotalGenerateGearPower,
                    StopReason = ToCamelCase(cache.GearNetwork.StopReason.ToString()),
                };
            }
            if (cache.FilterSplitter != null)
            {
                var directions = new List<FilterSplitterDirectionDto>();
                foreach (var d in cache.FilterSplitter.Directions)
                {
                    var itemIds = new List<int>();
                    foreach (var id in d.FilterItemIds) itemIds.Add(id.AsPrimitive());
                    directions.Add(new FilterSplitterDirectionDto { Mode = ToCamelCase(d.Mode.ToString()), FilterItemIds = itemIds });
                }
                dto.FilterSplitter = new FilterSplitterDto
                {
                    DirectionCount = cache.FilterSplitter.DirectionCount,
                    FilterSlotCountPerDirection = cache.FilterSplitter.FilterSlotCountPerDirection,
                    Directions = directions,
                };
            }
        }

        internal static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
