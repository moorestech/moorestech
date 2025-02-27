using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Train.RailGraph;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>
    ///     RailComponentを1つをロードセーブするテスト
    /// </summary>
    public class TralRailSaveLoadTest
    {
        [Test]
        public void TralRailOneBlockSaveLoadTest()
        {
            // 1. DIコンテナ生成
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);

            // 2. 必要な参照を取得
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();

            // 3. ブロックマスタ上のRailBlockIdを用意
            // 4. レールブロック設置
            var pos = new Vector3Int(10, 0, 10);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainRail, pos, BlockDirection.North, out var railBlock);

            // 5. RailSaverComponentを取得
            var railSaverComponent = railBlock.GetComponent<RailSaverComponent>();
            //railComponent.SetMemoString("This is rail-12345");

            /*
            var save = chest.GetSaveState();
            var states = new Dictionary<string, string>() { { chest.SaveKey, save } };
            Debug.Log(save);

            var chestBlock2 = blockFactory.Load(blockGuid, new BlockInstanceId(1), states, chestPosInfo);
            var chest2 = chestBlock2.GetComponent<VanillaChestComponent>();
            */


            // 6. セーブ実行
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log("[RailComponentSaveLoadTest] SaveJson:\n" + json);

            // 7. 一旦ワールドからブロック削除(テスト用)
            worldBlockDatastore.RemoveBlock(pos);
            //接続情報もリセット
            RailGraphDatastore.ResetInstance();

            // 8. ロードのDIコンテナ再生成
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            var loadJson = loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            // 9. ロード
            loadJson.Load(json);

            // 10. ロード後にRailBlockを再取得
            var loadedRailBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.IsNotNull(loadedRailBlock, "RailBlockが正しくロードされていません");

            // 11. セーブしたメモ文字列が一致しているか確認
            var loadedRailComp = loadedRailBlock.GetComponent<RailSaverComponent>();
            Assert.IsNotNull(loadedRailComp, "ロード後のRailComponentがnullです");

            var isdestroy = loadedRailComp.RailComponents[0].IsDestroy;
            Assert.AreEqual(false, isdestroy,
                "RailComponentが読み込まれませんでした");
        }
    }
}
