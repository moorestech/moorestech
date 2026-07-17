using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class FluidPipeSaveLoadTest
    {
        [Test]
        public void NormalSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var fluidPipePosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var fluidPipeBlock = blockFactory.Create(ForUnitTestModBlockId.FluidPipe, new BlockInstanceId(1), fluidPipePosInfo);
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();

            // シミュレーションノードへ直接液体を設定
            // Set fluid directly on the simulation node
            var fluidId = FluidTest.FluidId;
            const double fluidAmount = 50.5;
            fluidPipe.Node.FluidId = fluidId;
            fluidPipe.Node.Amount = fluidAmount;

            // セーブデータ取得
            // Fetch the save state
            var saveComponent = fluidPipeBlock.GetComponent<FluidPipeSaveComponent>();
            var saveText = saveComponent.GetSaveState();
            var states = new Dictionary<string, string> { { saveComponent.SaveKey, saveText } };
            Debug.Log(saveText);

            // セーブデータをロード
            // Load from the save state
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidPipe).BlockGuid;
            var loadedFluidPipeBlock = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, fluidPipePosInfo);
            var loadedFluidPipe = loadedFluidPipeBlock.GetComponent<FluidPipeComponent>();

            // 液体の状態が一致するかチェック
            // Verify the fluid state matches
            Assert.AreEqual(fluidId, loadedFluidPipe.Node.FluidId);
            Assert.AreEqual(fluidAmount, loadedFluidPipe.Node.Amount);
        }

        [Test]
        public void EmptyPipeSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var fluidPipePosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var fluidPipeBlock = blockFactory.Create(ForUnitTestModBlockId.FluidPipe, new BlockInstanceId(1), fluidPipePosInfo);
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();

            // 空の状態を確認
            // Confirm the pipe starts empty
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidPipe.Node.FluidId);
            Assert.AreEqual(0, fluidPipe.Node.Amount);

            // セーブデータ取得
            // Fetch the save state
            var saveComponent = fluidPipeBlock.GetComponent<FluidPipeSaveComponent>();
            var saveText = saveComponent.GetSaveState();
            var states = new Dictionary<string, string> { { saveComponent.SaveKey, saveText } };
            Debug.Log(saveText);

            // セーブデータをロード
            // Load from the save state
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidPipe).BlockGuid;
            var loadedFluidPipeBlock = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, fluidPipePosInfo);
            var loadedFluidPipe = loadedFluidPipeBlock.GetComponent<FluidPipeComponent>();

            // 空の状態が維持されているかチェック
            // Verify the empty state is preserved
            Assert.AreEqual(FluidMaster.EmptyFluidId, loadedFluidPipe.Node.FluidId);
            Assert.AreEqual(0, loadedFluidPipe.Node.Amount);
        }

        [Test]
        // 隣接パイプ間で生じた面速度がセーブ・ロード後も引き継がれることを確認
        // Face velocities between adjacent pipes survive a save-and-load round trip
        public void FaceVelocitySaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.zero, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);

            var pipe0 = pipeBlock0.GetComponent<FluidPipeComponent>();
            pipe0.AddLiquid(new FluidStack(30d, FluidTest.FluidId), default);

            // 数tick進めて面速度を発生させる
            // Advance a few ticks to develop a face velocity
            for (var i = 0; i < 5; i++) GameUpdater.RunFrames(1);

            var saveComponent = pipeBlock0.GetComponent<FluidPipeSaveComponent>();
            var saveText = saveComponent.GetSaveState();
            Debug.Log(saveText);

            // 正準側(座標が小さいpipe0)の保存データに、+x方向の非ゼロ面速度が含まれる
            // The canonical side's (pipe0, smaller position) save data holds a nonzero +x face velocity
            var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(saveText);
            Assert.AreEqual(1, jsonObject.FaceVelocities.Count);
            Assert.AreEqual(1, jsonObject.FaceVelocities[0].X);
            Assert.Greater(jsonObject.FaceVelocities[0].Velocity, 0);

            // ロード復元で初期速度としてノードに引き継がれる
            // Loading restores it as the face's initial velocity
            var restored = jsonObject.ToFaceVelocityDictionary();
            Assert.AreEqual(jsonObject.FaceVelocities[0].Velocity, restored[new Vector3Int(1, 0, 0)]);
        }
    }
}
