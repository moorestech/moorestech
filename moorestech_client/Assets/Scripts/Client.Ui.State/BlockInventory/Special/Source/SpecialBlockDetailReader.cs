using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
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
        public static SpecialBlockDetailViewModel Read(BlockGameObject block, BlockNetworkInfoCache networkCache, BeltTransportClientCache beltCache, out string gearAnomaly)
        {
            var param = block.BlockMasterElement.BlockParam;
            var gearDetail = ReadGear(out gearAnomaly);
            return new SpecialBlockDetailViewModel(
                ReadElectricToGear(), ReadGearToElectric(), gearDetail, BeltThroughputReader.Read(block, beltCache),
                SpecialNetworkReader.Electric(networkCache.Electric), SpecialNetworkReader.Gear(networkCache.GearNetwork), SpecialNetworkReader.FilterSplitter(networkCache.FilterSplitter),
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

            RocketLaunchPadDetail ReadRocketLaunchPad()
            {
                if (param is not RocketLaunchPadBlockParam rocketParam) return null;
                var state = block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは本文が「状態の取得を待っています」を出す / 録画（R-12）の発射台の段で気づく
                if (state == null) return null;
                var layout = new RocketLaunchPadSlotLayout(rocketParam);
                // 資材・積荷の枠ごとに要求の 1 品を Shift の束縛にする（本文の枠と同じ layout から組む）
                // Bind each material/payload slot to its one required item for Shift (from the same layout as the body slots)
                var inletBindings = layout.MaterialSlots.Concat(layout.PayloadSlots).Select(slot => new InletSlotBinding(slot, new[] { layout.RequirementAt(slot).ItemId })).ToList();
                return SpecialStateDetailReader.RocketLaunchPad(state, layout.MaterialSlots.ToList(), layout.PayloadSlots.ToList(), inletBindings, layout.OutputSlotStart);
            }

            SpaceElevatorDetail ReadSpaceElevator()
            {
                if (param is not SpaceElevatorBaseBlockParam elevatorParam) return null;
                var state = block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
                // silent-ok: 状態の初回到着待ちは区画が取得待ちを出す / 録画（R-12）の宇宙エレベーターの段で気づく
                if (state == null) return null;
                return SpecialStateDetailReader.SpaceElevator(state, SpaceElevatorBaseInventoryComponent.SatelliteSlot, SpaceElevatorBaseInventoryComponent.GetCarriedPayloadItemIds(elevatorParam), SpaceElevatorBaseInventoryComponent.FirstOutputSlot, elevatorParam.OutputSlotCount, elevatorParam.CablesPerElevator);
            }

            #endregion
        }
    }
}
