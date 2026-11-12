using System;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.BlockInventory.Special.Details;
using Game.Block.Interface.State;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の区画の selector の L0（出る条件は自分の詳細があるときだけ。DC-35・D-19）
    /// L0 of the special section selectors (a section shows only with its own detail; DC-35, D-19)
    /// </summary>
    public class SpecialDetailSelectorTest
    {
        [TestCase("empty", false)]
        [TestCase("gear", true)]
        [TestCase("allButGear", false)]
        public void 歯車の区画は歯車の詳細があるときだけ取れる(string detail, bool expected)
        {
            Assert.AreEqual(expected, new GearDetailSelector().TryGet(Detail(detail), out _));
        }

        [TestCase("empty", false)]
        [TestCase("gearToElectric", true)]
        [TestCase("allButGearToElectric", false)]
        public void 回転発電機の区画は回転発電機の詳細があるときだけ取れる(string detail, bool expected)
        {
            Assert.AreEqual(expected, new GearToElectricDetailSelector().TryGet(Detail(detail), out _));
        }

        [TestCase("empty", false)]
        [TestCase("electricNetwork", true)]
        [TestCase("allButElectricNetwork", false)]
        public void 電力網の区画は電力網の詳細があるときだけ取れる(string detail, bool expected)
        {
            Assert.AreEqual(expected, new ElectricNetworkDetailSelector().TryGet(Detail(detail), out _));
        }

        [TestCase("empty", false)]
        [TestCase("gearNetwork", true)]
        [TestCase("allButGearNetwork", false)]
        public void 歯車網の区画は歯車網の詳細があるときだけ取れる(string detail, bool expected)
        {
            Assert.AreEqual(expected, new GearNetworkDetailSelector().TryGet(Detail(detail), out _));
        }

        [Test]
        public void 取れた詳細は詳細の中身そのもの()
        {
            var expected = SpecialDetailSamples.GearMachineGear;
            Assert.IsTrue(new GearDetailSelector().TryGet(SpecialDetailSamples.Only(expected), out var gear));
            Assert.AreSame(expected, gear);
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
