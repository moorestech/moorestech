using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePoleSelectionTest
    {
        [Test]
        public void サイクルは末尾の次に先頭へ戻り前送りは先頭の前に末尾へ回る()
        {
            // 3種の電柱リストでインデックスの循環だけを検証する
            // Verify index wrap-around with a three-pole list
            var poles = new List<BlockId> { new(1), new(2), new(3) };
            var selection = new ElectricWirePoleSelection(poles);

            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CycleNext();
            selection.CycleNext();
            selection.CycleNext();
            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CyclePrevious();
            Assert.AreEqual(new BlockId(3), selection.SelectedBlockId);
        }
    }
}
