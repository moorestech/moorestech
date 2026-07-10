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

            // ハッチへ30注入し、時間経過で全量が隣のパイプへ流れる
            // Pour 30 into the hatch; over time the full amount flows into the next pipe
            var hatchPipeComponent = hatch.GetComponent<FluidPipeComponent>();
            var remain = hatchPipeComponent.AddLiquid(new FluidStack(30, FluidTest.FluidId), FluidContainer.Empty);
            Assert.AreEqual(0, remain.Amount);

            for (var i = 0; i < 60; i++) GameUpdater.UpdateOneTick();

            Assert.AreEqual(0, hatchPipeComponent.GetAmount(), 1);
            Assert.AreEqual(30, pipe.GetComponent<FluidPipeComponent>().GetAmount(), 1);
        }
    }
}
