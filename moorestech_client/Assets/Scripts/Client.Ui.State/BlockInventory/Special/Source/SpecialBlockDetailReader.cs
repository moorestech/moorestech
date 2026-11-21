using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Core.Master;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 開いたブロックの特殊族の詳細をまとめて読む（Web BlockDetailDtoBuilder の特殊族の部分と同じ源・同じ条件）
    /// Reads all special-family details of the open block (same sources and conditions as the special part of Web BlockDetailDtoBuilder)
    /// </summary>
    public static class SpecialBlockDetailReader
    {
        // gearAnomaly は歯車のマスタの食い違いの文面（無ければ null）。ログは同じ間 1 回だけ出すため呼び手（Model）が持つ
        // gearAnomaly is the gear master-mismatch text (null when none); the caller (Model) logs it once while it persists
        // unlockedPayloadItemIds は解放済みの積荷（発射台・拠点の一覧に出す種類を絞る）
        // unlockedPayloadItemIds are the unlocked cargo kinds, narrowing the pad and base lists
        public static SpecialBlockDetailViewModel Read(BlockGameObject block, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache, IReadOnlyCollection<ItemId> unlockedPayloadItemIds, out string gearAnomaly)
        {
            var param = block.BlockMasterElement.BlockParam;
            var gearDetail = ReadGear(out gearAnomaly);
            return new SpecialBlockDetailViewModel(
                ReadElectricToGear(), ReadGearToElectric(), gearDetail, BeltThroughputReader.Read(block, beltCache),
                SpecialNetworkReader.Electric(networkCache.Electric), SpecialNetworkReader.Gear(networkCache.GearNetwork), ReadFilterSplitter(),
                ReadRocketLaunchPad(), ReadSpaceElevator());

            #region Internal

            ElectricToGearDetail ReadElectricToGear()
            {
                if (param is not ElectricToGearGeneratorBlockParam electricParam) return null;
                var state = block.GetStateDetail<ElectricToGearGeneratorBlockStateDetail>(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ち。本文は空のまま（Web と同じ） / 届かないままなら録画（R-12）の回転生成機の段で気づく
                if (state == null) return null;
                var modes = electricParam.OutputModes.Select(mode => new ElectricToGearOutputMode(mode.Rpm, mode.Torque, mode.RequiredPower)).ToList();
                return SpecialStateDetailReader.ElectricToGear(state, modes);
            }

            GearToElectricDetail ReadGearToElectric()
            {
                if (param is not GearToElectricGeneratorBlockParam generatorParam) return null;
                var state = block.GetStateDetail<GearToElectricGeneratorBlockStateDetail>(GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは区画を出さない / 届かないままなら録画（R-12）の回転発電機の段で気づく
                if (state == null) return null;
                return SpecialStateDetailReader.GearToElectric(state, UiNumberFormat.FromServerFloat(generatorParam.MaxGeneratedPower));
            }

            GearDetail ReadGear(out string anomaly)
            {
                anomaly = null;
                // 歯車駆動の種類は共通の 1 か所で読む
                // Read gear-drive kind at the shared single place
                var gear = block.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
                var drive = GearDriveConversion.Read(gear, param);
                switch (drive.Kind)
                {
                    // silent-ok: 歯車でないブロックは歯車の状態を持たない / 歯車ブロックで出ないなら録画（R-12）の歯車の段で気づく
                    case GearDriveKind.Electric: return null;
                    case GearDriveKind.Generator: return SpecialStateDetailReader.Generator(gear);
                    case GearDriveKind.Consumer: return SpecialStateDetailReader.Consumer(gear, UiNumberFormat.FromServerFloat(ConsumptionOf(drive).GearConsumption.BaseRpm));
                    case GearDriveKind.InconsistentConsumer:
                        // 食い違いは歯車行なしで文面を返す
                        // Mismatch omits gear rows and returns the text
                        drive.TryGetAnomaly(out anomaly);
                        return null;
                    default: throw new ArgumentOutOfRangeException(nameof(drive.Kind), drive.Kind, null);
                }
            }

            IGearConsumptionParam ConsumptionOf(GearDriveReading drive)
            {
                // 種別が Consumer のときだけ呼ぶので必ず取れる
                // Called only for the Consumer kind, so it is always available
                drive.TryGetConsumption(out var consumption);
                return consumption;
            }

            FilterSplitterDetail ReadFilterSplitter()
            {
                // silent-ok: 分岐器でないブロックは分岐器の詳細を持たない（キャッシュも分岐器のときだけ取りに行く。BlockNetworkInfoCache.Track） / 分岐器で出ないなら録画の分岐器の段で気づく
                if (param is not FilterSplitterBlockParam splitterParam) return null;

                // 出口の側は開いた分岐器のマスタの接続で決まる（発射台の枠の並びをマスタから組むのと同じ）
                // Exit sides come from the open splitter's master connectors (same as building the launch pad slot layout from the master)
                return SpecialNetworkReader.FilterSplitter(networkCache.FilterSplitter, FilterSplitterOutputSideResolver.ResolveAll(splitterParam.InventoryConnectors));
            }

            RocketLaunchPadDetail ReadRocketLaunchPad()
            {
                if (param is not RocketLaunchPadBlockParam rocketParam) return null;
                var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは本文が「状態の取得を待っています」を出す / 録画（R-12）の発射台の段で気づく
                if (state == null) return null;
                var layout = new RocketLaunchPadSlotLayout(rocketParam);
                var materials = layout.MaterialSlots.Select(slot => Required(layout, slot)).ToList();
                var payloads = layout.PayloadSlots.Select(slot => Required(layout, slot)).ToList();
                // 一覧は積荷の枠の種類（積荷表の全種類）から、今の便は積み終えたロケットの積荷
                // The list comes from the payload slots' kinds (the whole payload table); the current flight is the loaded rocket's cargo
                var carried = OrbitalCargoCatalog.ForCarried(payloads.Select(payload => payload.ItemId).ToList());
                var cargo = new OrbitalCargoDetail(state.SelectedCargoItemId, state.LoadedCargoItemId, carried, unlockedPayloadItemIds, rocketParam.BuildSeconds);
                var inletBindings = OrbitalCargoSlotBinding.PadShiftBindings(materials, payloads, cargo);
                return SpecialStateDetailReader.RocketLaunchPad(state, materials, payloads, inletBindings, layout.OutputSlotStart, cargo);
            }

            RequiredItemSlot Required(RocketLaunchPadSlotLayout layout, int slot)
            {
                var requirement = layout.RequirementAt(slot);
                return new RequiredItemSlot(slot, requirement.ItemId, requirement.Count);
            }

            SpaceElevatorDetail ReadSpaceElevator()
            {
                if (param is not SpaceElevatorBaseBlockParam elevatorParam) return null;
                var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは区画が取得待ちを出す / 録画（R-12）の宇宙エレベーターの段で気づく
                if (state == null) return null;
                // 一覧は拠点が運ぶ種類から、今の便は昇降中の便の積荷
                // The list comes from the kinds the base carries; the current flight is the climbing flight's cargo
                var carried = OrbitalCargoCatalog.ForCarried(SpaceElevatorBaseInventoryComponent.GetCarriedPayloadItemIds(elevatorParam));
                var cargo = new OrbitalCargoDetail(state.SelectedCargoItemId, state.ClimbingCargoItemId, carried, unlockedPayloadItemIds, elevatorParam.ClimbSeconds);
                return SpecialStateDetailReader.SpaceElevator(state, SpaceElevatorBaseInventoryComponent.SatelliteSlot, SpaceElevatorBaseInventoryComponent.FirstOutputSlot, elevatorParam.OutputSlotCount, elevatorParam.CablesPerElevator, cargo);
            }

            #endregion
        }
    }
}
