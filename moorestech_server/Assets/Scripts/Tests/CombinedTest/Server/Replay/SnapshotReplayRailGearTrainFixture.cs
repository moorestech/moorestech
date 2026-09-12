using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.Replay
{
    // レール・歯車・列車を含む世界を組む。これらはロード経路でID採番のため乱数を引く唯一の系統で、含まない世界の検査は素通りする
    // Builds a world with rails, gears, and a train: the only subsystems whose load path draws randomness for id allocation, so a world without them checks nothing
    public static class SnapshotReplayRailGearTrainFixture
    {
        public static void BuildWorld(ServiceProvider provider, PacketResponseCreator packetResponseCreator)
        {
            var environment = new TrainTestEnvironment(provider, ServerContext.WorldBlockDatastore, packetResponseCreator);

            // 歯車網はロード直後の初回tick先頭で必ず再構築される
            // A gear network is always rebuilt at the first tick head right after a load
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(30, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.Shaft, new Vector3Int(31, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.SmallGear, new Vector3Int(32, 0, 0), BlockDirection.North);

            // レールブロック3本を直列に繋ぐ。RailNodeはコンストラクタでGuidを採番する
            // Chain three rail blocks; a RailNode allocates its GUID in its constructor
            var rail0 = TrainTestHelper.PlaceRail(environment, new Vector3Int(40, 0, 0), BlockDirection.North);
            var rail1 = TrainTestHelper.PlaceRail(environment, new Vector3Int(40, 0, 10), BlockDirection.North);
            var rail2 = TrainTestHelper.PlaceRail(environment, new Vector3Int(40, 0, 20), BlockDirection.North);
            rail0.FrontNode.ConnectNode(rail1.FrontNode, SegmentLength);
            rail1.FrontNode.ConnectNode(rail2.FrontNode, SegmentLength);

            // 列車1編成をレール上に置く。ダイヤ項目も復元時に1件につき1回乱数を引く
            // Put one train on the rails; each diagram entry also draws randomness once on restore
            var railNodes = new List<IRailNode> { rail1.FrontNode, rail0.FrontNode };
            var railPosition = new RailPosition(railNodes, TrainLength, SegmentLength / 2);
            var trainCar = new TrainCar(MasterHolder.TrainUnitMaster.Train.TrainCars.First(), true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);
            trainUnit.trainDiagram.AddEntry(rail2.FrontNode);
            trainUnit.trainDiagram.AddEntry(rail0.FrontNode);
        }

        // フィクスチャが実際にレール・歯車・列車を保存像へ載せていることを確かめる。載っていなければ検査は空振り
        // Verify the fixture really puts rails, gears, and a train into the save image; otherwise the check is vacuous
        public static void AssertRailGearTrainPresent(string snapshotJson)
        {
            var root = JObject.Parse(snapshotJson);
            Assert.IsNotEmpty(root["railSegments"].Children(), "レールセグメントが保存像に無い。フィクスチャが効いていない");
            Assert.IsNotEmpty(root["trainUnits"].Children(), "列車が保存像に無い。フィクスチャが効いていない");
            Assert.IsNotEmpty(root["trainUnits"][0]["Diagram"]["Entries"].Children(), "ダイヤ項目が保存像に無い。フィクスチャが効いていない");
        }

        private const int SegmentLength = 1000;
        private const int TrainLength = 4;
    }
}
