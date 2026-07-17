using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.World.Interface.DataStore;
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

        [Test]
        // 保存した面速度が、実ロード経路（LoadBlockDataList→トポロジ再構築）で実際に面へシードされることを確認
        // Saved face velocities are actually seeded into faces through the real load path (LoadBlockDataList → topology rebuild)
        public void FaceVelocityLoadRestoreTest()
        {
            // 保存側ワールドで隣接パイプ間に面速度を発生させ、両パイプのセーブ状態を取得する
            // In the source world, develop a face velocity between adjacent pipes and capture both pipes' save states
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.zero, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var pipeBlock1);

            pipeBlock0.GetComponent<FluidPipeComponent>().AddLiquid(new FluidStack(30d, FluidTest.FluidId), default);
            for (var i = 0; i < 5; i++) GameUpdater.RunFrames(1);

            var savedJson = Newtonsoft.Json.JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(
                pipeBlock0.GetComponent<FluidPipeSaveComponent>().GetSaveState());
            var savedVelocity = savedJson.FaceVelocities[0].Velocity;
            Assert.Greater(savedVelocity, 0);

            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidPipe).BlockGuid;
            var blockJsonObjects = new List<BlockJsonObject>
            {
                new(Vector3Int.zero, blockGuid.ToString(), 1, pipeBlock0.GetSaveState(), (int)BlockDirection.North),
                new(Vector3Int.right, blockGuid.ToString(), 2, pipeBlock1.GetSaveState(), (int)BlockDirection.North),
            };

            // 新しいワールドへ実ロード経路で復元し、tickを回さずに再構築だけ実行する
            // Restore into a fresh world through the real load path, then rebuild without advancing any tick
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.LoadBlockDataList(blockJsonObjects);

            var fluidNetworkDatastore = ServerContext.GetService<FluidNetworkDatastore>();
            fluidNetworkDatastore.RebuildIfDirty();

            // 正準側(pipe0)が所有する+x方向の面に、保存値どおりの初期速度が乗っている
            // The +x face owned by the canonical side (pipe0) carries exactly the saved initial velocity
            var loadedPipe0 = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<FluidPipeComponent>();
            var faceVelocities = new List<(Vector3Int direction, double velocity)>();
            fluidNetworkDatastore.CollectOwnedFaceVelocities(loadedPipe0, faceVelocities);

            Assert.AreEqual(1, faceVelocities.Count);
            Assert.AreEqual(new Vector3Int(1, 0, 0), faceVelocities[0].direction);
            Assert.AreEqual(savedVelocity, faceVelocities[0].velocity, 1e-9);
        }
    }
}
