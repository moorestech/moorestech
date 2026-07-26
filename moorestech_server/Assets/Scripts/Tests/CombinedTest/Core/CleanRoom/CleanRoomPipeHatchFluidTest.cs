using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface.Extension;
using Game.Fluid;
using NUnit.Framework;
using Tests.CombinedTest.Core;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomPipeHatchFluidTest
    {
        [Test]
        public void PipeHatchTransfersFluidToConnectedPipeTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 北向きパイプハッチの排出面(+z)に鉄のパイプを接続する
            // Connect an iron pipe to the outflow face (+z) of a north-facing pipe hatch
            var hatch = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, new Vector3Int(0, 0, 0));
            var pipe = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1));

            // ハッチへ30注入する。ハッチは+z方向への一方向流出のため、逆流せず「ハッチ≦パイプ」の水位で静定する
            // Pour 30 into the hatch; the hatch only flows outward (+z), so it settles with hatch level <= pipe level and no backflow
            var hatchPipeComponent = hatch.GetComponent<FluidPipeComponent>();
            var remain = hatchPipeComponent.AddLiquid(new FluidStack(30, FluidTest.FluidId), default);
            Assert.AreEqual(0, remain.Amount);

            // 静定まで進める（10秒 = 200 tick）
            // Advance until settled (10 seconds = 200 ticks)
            for (var i = 0; i < 200; i++) GameUpdater.UpdateOneTick();

            var hatchAmount = hatchPipeComponent.GetAmount();
            var pipeAmount = pipe.GetComponent<FluidPipeComponent>().GetAmount();

            // 半量以上がパイプへ渡り、逆流がなく、総量は厳密に保存される
            // At least half crosses into the pipe, nothing flows back, and the total is exactly conserved
            Assert.GreaterOrEqual(pipeAmount, 15);
            Assert.LessOrEqual(hatchAmount, pipeAmount + 0.0001);
            Assert.AreEqual(30, hatchAmount + pipeAmount, 0.0001);
        }
    }
}
