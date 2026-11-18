using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Game.Block.Blocks.Machine;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// サーバーの機械の状態文字列が表示の状態へ写る
    /// The server's machine state strings map onto the display state
    /// </summary>
    public class MachineStateTypeConversionTest
    {
        [TestCase(VanillaMachineBlockStateConst.IdleState, MachineProcessDisplayState.Idle)]
        [TestCase(VanillaMachineBlockStateConst.ProcessingState, MachineProcessDisplayState.Processing)]
        [TestCase(VanillaMachineBlockStateConst.HaltedState, MachineProcessDisplayState.Halted)]
        public void 状態の定数が表示の状態へ写る(string wire, MachineProcessDisplayState expected)
        {
            Assert.IsTrue(MachineStateTypeConversion.TryConvert(wire, out var state));
            Assert.AreEqual(expected, state);
        }

        [Test]
        public void 知らない文字列は偽を返す()
        {
            Assert.IsFalse(MachineStateTypeConversion.TryConvert("unknown", out _));
            Assert.IsFalse(MachineStateTypeConversion.TryConvert(null, out _));
        }
    }
}
