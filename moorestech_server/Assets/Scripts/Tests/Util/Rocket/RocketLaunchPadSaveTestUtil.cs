using System.Linq;
using Core.Update;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Blocks.RocketLaunchPad.Save;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util.Rocket
{
    // - 実ワールドを保存
    // - 別DIでブロック状態を復元
    // - Save the real world
    // - Restore block state through a separate DI container
    public static class RocketLaunchPadSaveTestUtil
    {
        public static JObject CreateSavedWorld(RocketLaunchPadPhase phase)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            if (phase != RocketLaunchPadPhase.AwaitingBuildCommand)
            {
                processor.RequestBuild();
                if (phase == RocketLaunchPadPhase.Building) GameUpdater.RunFrames(7);
                else
                {
                    Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
                    if (phase == RocketLaunchPadPhase.ReadyToLaunch)
                    {
                        RocketLaunchPadTestUtil.FillOutputSlots(pad, 100);
                        processor.RequestLaunch();
                        GameUpdater.RunFrames(1);
                    }
                }
                // 残した入れ口の中身も保存往復で検査する
                // Also verify input contents left behind through the save round trip
                RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.CablePayloadId, 5);
            }
            Assert.AreEqual(phase, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            return JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(services));
        }

        public static JObject State(JObject save)
        {
            var block = save["world"].OfType<JObject>().Single(value => value["state"]?[typeof(RocketLaunchPadProcessorComponent).FullName] != null);
            return (JObject)block["state"][typeof(RocketLaunchPadProcessorComponent).FullName];
        }

        public static IBlock Load(JObject save)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            SaveLoadJsonTestHelper.LoadFromJson(services, save.ToString());
            return ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
        }

        public static RocketLaunchPadSaveJsonObject Capture(IBlock pad)
        {
            return (RocketLaunchPadSaveJsonObject)pad.GetComponent<RocketLaunchPadProcessorComponent>().GetSaveState();
        }

        public static void ClearOutputs(IBlock pad)
        {
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            for (var i = 5; i < 7; i++) inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
        }
    }
}
