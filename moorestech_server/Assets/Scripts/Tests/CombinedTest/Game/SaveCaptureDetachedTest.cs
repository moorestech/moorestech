using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Game
{
    // 取り込んだ保存像が生きた参照を含んでいれば、取り込み後の世界変更が後からのJSON化に混ざる
    // If a captured image held live references, world changes after capture would leak into a later serialization
    public class SaveCaptureDetachedTest
    {
        [Test]
        public void 取り込み後に世界を変えても直列化結果は変わらない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(6, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            SetItem(GetInventory(serviceProvider), 0, MasterHolder.ItemMaster.GetItemMaster(new ItemId(1)).ItemGuid, 5);

            var assembler = serviceProvider.GetRequiredService<AssembleSaveJsonText>();
            var captured = assembler.Capture();
            var immediately = AssembleSaveJsonText.Serialize(captured);

            // 取り込み後にあらゆる種類の変更を加える
            // Apply every kind of mutation after the capture
            world.RemoveBlock(new Vector3Int(0, 0), BlockRemoveReason.ManualRemove);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(9, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            SetItem(GetInventory(serviceProvider), 0, MasterHolder.ItemMaster.GetItemMaster(new ItemId(2)).ItemGuid, 9);
            // スキット登録はtickスレッドがdatastoreの生リストを直接足す経路で、取り込み像が生参照だと後から混ざる
            // Skit registration appends straight into the datastore's list, so a live reference in the image would leak it
            serviceProvider.GetRequiredService<ChallengeDatastore>().CurrentChallengeInfo.PlayedSkitIds.Add("skit-after-capture");
            for (var i = 0; i < 20; i++) GameUpdater.UpdateOneTick();

            var later = AssembleSaveJsonText.Serialize(captured);
            Assert.AreEqual(immediately, later, "取り込んだ保存像が後の世界変更で書き換わった（生きた参照が残っている）");

            // 変更が保存像に現れる種類のものであることを確かめ、検査が空振りしないようにする
            // Confirm the mutations do surface in a save image so this check cannot pass vacuously
            var recaptured = AssembleSaveJsonText.Serialize(assembler.Capture());
            Assert.AreNotEqual(immediately, recaptured, "世界を変えても取り込み直した保存像が変わらない（検査が空振りしている）");
        }
    }
}
