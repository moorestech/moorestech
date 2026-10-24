using System;
using System.Collections.Generic;
using Client.Game.InGame.CleanRoom;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Tests.CleanRoom
{
    public class CleanRoomTooltipLinesTest
    {
        [Test]
        public void EnclosedRoomsProduceClassAirAndFilterLinesPerRoomTest()
        {
            // 共有壁は2部屋分の3行ずつを出し、Out の部屋はクラス未達の行になる
            // A shared wall yields three lines per room, and an Out room yields the below-class line
            var filter = new CleanRoomAirFilterMessagePack(new Vector3IntMessagePack(1, 1, 1), 1d, 10d, 5, 1d);
            var classA = CleanRoomSummaryMessagePack.CreateClassified("A", 0, 0.1234d, 2d, 26, new List<CleanRoomAirFilterMessagePack> { filter });
            var outRoom = CleanRoomSummaryMessagePack.CreateOut(3d, 0d, 26, new List<CleanRoomAirFilterMessagePack>());
            var view = new CleanRoomEnclosedView(new List<CleanRoomSummaryMessagePack> { classA, outRoom });

            var lines = CleanRoomTooltipLines.Build(view);
            Assert.AreEqual(6, lines.Length);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomClass.Key, lines[0].Key.Key);
            CollectionAssert.AreEqual(new[] { "A" }, lines[0].TextParams);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomAir.Key, lines[1].Key.Key);
            CollectionAssert.AreEqual(new[] { "0.123", "2.000" }, lines[1].TextParams);
            CollectionAssert.AreEqual(new[] { "1" }, lines[2].TextParams);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomClassOut.Key, lines[3].Key.Key);
            CollectionAssert.AreEqual(new[] { "0" }, lines[5].TextParams);
        }

        [Test]
        public void OpeningsLeakShowsTotalCountTest()
        {
            // 表示上限で切ったセル数でなく総数を出す
            // Show the total count rather than the number of cells capped for display
            var cells = new List<Vector3IntMessagePack> { new(0, 0, 0), new(1, 0, 0) };
            AssertSingleLeakLine(new CleanRoomOpeningsLeakView(cells, 7), "ui.tooltip.cleanRoomNotSealed", new[] { "7" });
        }

        [Test]
        public void TooLargeLeakShowsVolumeCapTest()
        {
            AssertSingleLeakLine(new CleanRoomTooLargeLeakView(4096), "ui.tooltip.cleanRoomTooLarge", new[] { "4096" });
        }

        [Test]
        public void NoEnclosureLeakProducesOneLineTest()
        {
            AssertSingleLeakLine(CleanRoomNoEnclosureLeakView.Instance, "ui.tooltip.cleanRoomNoEnclosure", Array.Empty<string>());
        }

        [Test]
        public void DetectionPendingLeakProducesOneLineTest()
        {
            AssertSingleLeakLine(CleanRoomDetectionPendingLeakView.Instance, "ui.tooltip.cleanRoomDetecting", Array.Empty<string>());
        }

        private static void AssertSingleLeakLine(CleanRoomLeakView leak, string expectedKey, string[] expectedParams)
        {
            var lines = CleanRoomTooltipLines.Build(new CleanRoomNotEnclosedView(leak));
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(expectedKey, lines[0].Key.Key);
            CollectionAssert.AreEqual(expectedParams, lines[0].TextParams);
        }
    }
}
