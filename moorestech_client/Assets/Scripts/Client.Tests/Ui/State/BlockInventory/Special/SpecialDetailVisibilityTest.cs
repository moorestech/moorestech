using System;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Game.Block.Interface.State;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の区画の出る条件の表（View の Render が偽を返していた条件をそのまま移したもの。DC-35）
    /// Table of when each special section shows (the conditions the views' Render used to return false on, moved as is; DC-35)
    /// </summary>
    public class SpecialDetailVisibilityTest
    {
        // 区画は自分の詳細があるときだけ出し、他の詳細だけでは出ない
        // A section shows only with its own detail; other details alone never show it
        [TestCase(SpecialDetailSection.Gear, "empty", false)]
        [TestCase(SpecialDetailSection.Gear, "gear", true)]
        [TestCase(SpecialDetailSection.Gear, "allButGear", false)]
        [TestCase(SpecialDetailSection.GearToElectric, "empty", false)]
        [TestCase(SpecialDetailSection.GearToElectric, "gearToElectric", true)]
        [TestCase(SpecialDetailSection.GearToElectric, "allButGearToElectric", false)]
        [TestCase(SpecialDetailSection.ElectricNetwork, "empty", false)]
        [TestCase(SpecialDetailSection.ElectricNetwork, "electricNetwork", true)]
        [TestCase(SpecialDetailSection.ElectricNetwork, "allButElectricNetwork", false)]
        [TestCase(SpecialDetailSection.GearNetwork, "empty", false)]
        [TestCase(SpecialDetailSection.GearNetwork, "gearNetwork", true)]
        [TestCase(SpecialDetailSection.GearNetwork, "allButGearNetwork", false)]
        // 本文と題の操作は詳細の未着でも出す（未着の見え方は View が中で持つ）
        // Bodies and the title action show even before the detail arrives (the view owns the waiting look)
        [TestCase(SpecialDetailSection.Body, "empty", true)]
        [TestCase(SpecialDetailSection.Body, "everything", true)]
        [TestCase(SpecialDetailSection.TitleAction, "empty", true)]
        [TestCase(SpecialDetailSection.TitleAction, "everything", true)]
        public void 区画ごとの出る条件は今と同じ(SpecialDetailSection section, string detail, bool expected)
        {
            Assert.AreEqual(expected, SpecialDetailVisibility.Shows(section, Detail(detail)));
        }

        [Test]
        public void 未知の区画は例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpecialDetailVisibility.Shows((SpecialDetailSection)99, SpecialBlockDetailViewModel.Empty));
        }

        private static SpecialBlockDetailViewModel Detail(string kind)
        {
            var electricToGear = SpecialDetailSamples.ElectricToGear;
            var gearToElectric = SpecialDetailSamples.GearToElectricGenerating;
            var gear = SpecialDetailSamples.GearMachineGear;
            var belt = SpecialDetailSamples.Belt;
            var electricNetwork = SpecialDetailSamples.PoleNetwork;
            var gearNetwork = SpecialDetailSamples.GearMachineNetwork;
            var filter = SpecialDetailSamples.FilterSplitter;
            var rocket = SpecialDetailSamples.Rocket(RocketLaunchPadPhase.Building);
            var elevator = SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.Climbing, true, 1000, 0f);
            switch (kind)
            {
                case "empty": return SpecialBlockDetailViewModel.Empty;
                case "gear": return SpecialDetailSamples.Only(gear);
                case "gearToElectric": return SpecialDetailSamples.Only(gearToElectric);
                case "electricNetwork": return SpecialDetailSamples.Only(electricNetwork);
                case "gearNetwork": return SpecialDetailSamples.Only(gearNetwork);
                case "everything": return new SpecialBlockDetailViewModel(electricToGear, gearToElectric, gear, belt, electricNetwork, gearNetwork, filter, rocket, elevator);
                case "allButGear": return new SpecialBlockDetailViewModel(electricToGear, gearToElectric, null, belt, electricNetwork, gearNetwork, filter, rocket, elevator);
                case "allButGearToElectric": return new SpecialBlockDetailViewModel(electricToGear, null, gear, belt, electricNetwork, gearNetwork, filter, rocket, elevator);
                case "allButElectricNetwork": return new SpecialBlockDetailViewModel(electricToGear, gearToElectric, gear, belt, null, gearNetwork, filter, rocket, elevator);
                case "allButGearNetwork": return new SpecialBlockDetailViewModel(electricToGear, gearToElectric, gear, belt, electricNetwork, null, filter, rocket, elevator);
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
