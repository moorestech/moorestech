using System.Collections.Generic;
using System.Reflection;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var blockFactory = ServerContext.BlockFactory;
            var fluidPipePosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var fluidPipeBlock = blockFactory.Create(ForUnitTestModBlockId.FluidPipe, new BlockInstanceId(1), fluidPipePosInfo);
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();
            
            //リフレクションで_fluidContainerを取得
            var fluidContainerField = typeof(FluidPipeComponent).GetField("_fluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            var fluidContainer = (FluidContainer)fluidContainerField.GetValue(fluidPipe);
            
            //液体を設定
            var fluidId = FluidTest.FluidId;
            const double fluidAmount = 50.5;
            fluidContainer.FluidId = fluidId;
            fluidContainer.Amount = fluidAmount;
            
            //セーブデータ取得
            var saveComponent = fluidPipeBlock.GetComponent<FluidPipeSaveComponent>();
            var saveText = saveComponent.GetSaveState();
            var states = new Dictionary<string, string>() { { saveComponent.SaveKey, saveText } };
            Debug.Log(saveText);
            
            //セーブデータをロード
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidPipe).BlockGuid;
            var loadedFluidPipeBlock = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, fluidPipePosInfo);
            var loadedFluidPipe = loadedFluidPipeBlock.GetComponent<FluidPipeComponent>();
            
            //液体の状態が一致するかチェック
            var loadedFluidContainer = (FluidContainer)fluidContainerField.GetValue(loadedFluidPipe);
            Assert.AreEqual(fluidId, loadedFluidContainer.FluidId);
            Assert.AreEqual(fluidAmount, loadedFluidContainer.Amount);
        }
        
        [Test]
        public void EmptyPipeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var blockFactory = ServerContext.BlockFactory;
            var fluidPipePosInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var fluidPipeBlock = blockFactory.Create(ForUnitTestModBlockId.FluidPipe, new BlockInstanceId(1), fluidPipePosInfo);
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();
            
            //リフレクションで_fluidContainerを取得
            var fluidContainerField = typeof(FluidPipeComponent).GetField("_fluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            var fluidContainer = (FluidContainer)fluidContainerField.GetValue(fluidPipe);
            
            //空の状態を確認
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidContainer.FluidId);
            Assert.AreEqual(0, fluidContainer.Amount);
            
            //セーブデータ取得
            var saveComponent = fluidPipeBlock.GetComponent<FluidPipeSaveComponent>();
            var saveText = saveComponent.GetSaveState();
            var states = new Dictionary<string, string>() { { saveComponent.SaveKey, saveText } };
            Debug.Log(saveText);
            
            //セーブデータをロード
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidPipe).BlockGuid;
            var loadedFluidPipeBlock = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, fluidPipePosInfo);
            var loadedFluidPipe = loadedFluidPipeBlock.GetComponent<FluidPipeComponent>();
            
            //空の状態が維持されているかチェック
            var loadedFluidContainer = (FluidContainer)fluidContainerField.GetValue(loadedFluidPipe);
            Assert.AreEqual(FluidMaster.EmptyFluidId, loadedFluidContainer.FluidId);
            Assert.AreEqual(0, loadedFluidContainer.Amount);
        }
    }
}