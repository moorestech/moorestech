using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using System;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// BlockGameObject の StateDetail 群とマスタ値から capability 詳細を dto へ充填する
    /// Fills capability details into the dto from the BlockGameObject's state details and master values
    /// </summary>
    public static class BlockDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache)
        {
            var param = block.BlockMasterElement.BlockParam;
            ElectricToGearDetailDtoBuilder.Apply(dto, block, param);
            TrainPlatformDetailDtoBuilder.Apply(dto, block, param);

            // 機械系: CommonMachine + MachineBlock の両 StateDetail が揃うブロックのみ
            // Machines: only blocks carrying both CommonMachine and MachineBlock state details
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var machineState = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (common != null && machineState != null && param is IMachineParam machineParam)
            {
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(machineState.MachineRecipeGuid));
                dto.Progress = machineState.ProcessingRate;
                dto.Machine = new MachineDetailDto
                {
                    RecipeGuid = machineState.MachineRecipeGuid,
                    RecipeTime = recipe?.Time ?? 0,
                    OutputItems = BuildMachineOutputItems(recipe),
                    CurrentState = ToCamelCase(common.CurrentStateType),
                    CurrentPower = common.CurrentPower,
                    RequestPower = common.RequestPower,
                    SlotLayout = new SlotLayoutDto { Input = machineParam.InputSlotCount, Output = machineParam.OutputSlotCount, Module = machineParam.ModuleSlotCount },
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

            // 採掘機: CommonMiner StateDetail + マスタ MineSettings から分間採掘数を算出
            // Miners: the CommonMiner state detail plus per-minute rates derived from master MineSettings
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            if (miner != null && common != null && param is IMinerParam minerParam)
            {
                dto.Progress = common.ProcessingRate;
                dto.Miner = new MinerDetailDto
                {
                    CurrentPower = common.CurrentPower,
                    RequestPower = common.RequestPower,
                    MiningItems = BuildMiningItems(miner, minerParam),
                };
            }

            // ギア: GearStateDetail + マスタ GearConsumption（要求値）
            // Gears: the GearStateDetail plus master GearConsumption requirements
            var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (gear != null)
            {
                var consumption = GetGearConsumption(param);
                dto.Gear = new GearDetailDto
                {
                    IsClockwise = gear.IsClockwise,
                    CurrentRpm = gear.CurrentRpm,
                    CurrentTorque = gear.CurrentTorque,
                    BaseRpm = consumption != null ? (float)consumption.BaseRpm : 0f,
                    BaseTorque = consumption != null ? (float)consumption.BaseTorque : 0f,
                };
            }

            // 液体スロット: FluidMachineInventory StateDetail（入力→出力の順で連結）
            // Fluid slots: the FluidMachineInventory state detail (inputs then outputs)
            var fluid = block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
            if (fluid != null)
            {
                AppendFluidSlots(dto.FluidSlots, fluid.InputTanks);
                AppendFluidSlots(dto.FluidSlots, fluid.OutputTanks);
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

        private static List<MiningItemDto> BuildMiningItems(CommonMinerBlockStateDetail miner, IMinerParam minerParam)
        {
            // uGUI MinerBlockInventoryView.cs:126-140 と同じ算出（60/Time を分間数に）
            // Same derivation as uGUI MinerBlockInventoryView.cs:126-140 (60/Time per minute)
            var result = new List<MiningItemDto>();
            var currentIds = miner.GetCurrentMiningItemIds();
            foreach (var settings in minerParam.MineSettings.items)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(settings.ItemGuid);
                if (!currentIds.Contains(itemId)) continue;
                if (settings.Time <= 0f) continue;
                result.Add(new MiningItemDto { ItemId = itemId.AsPrimitive(), ItemsPerMinute = 60f / (float)settings.Time });
            }
            return result;
        }

        private static void AppendFluidSlots(List<BlockFluidSlotDto> slots, List<FluidMessagePack> tanks)
        {
            foreach (var tank in tanks)
            {
                // 空流体は名前空文字（uGUI MachineBlockInventoryView の EmptyFluidId 分岐と同じ扱い）
                // Empty fluids get an empty name (same handling as the EmptyFluidId branch in uGUI MachineBlockInventoryView)
                var isEmpty = new FluidId(tank.FluidId) == FluidMaster.EmptyFluidId;
                var name = isEmpty ? "" : MasterHolder.FluidMaster.GetFluidMaster(new FluidId(tank.FluidId)).Name;
                slots.Add(new BlockFluidSlotDto { FluidId = tank.FluidId, Amount = tank.Amount, Capacity = tank.MaxCapacity, Name = name });
            }
        }

        private static GearConsumption GetGearConsumption(object param)
        {
            // ギア消費要求値を持つ param のみ対象（GearMachine / GearMiner）
            // Only params carrying gear consumption requirements (GearMachine / GearMiner)
            return param switch
            {
                GearMachineBlockParam p => p.GearConsumption,
                GearMinerBlockParam p => p.GearConsumption,
                _ => null,
            };
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
