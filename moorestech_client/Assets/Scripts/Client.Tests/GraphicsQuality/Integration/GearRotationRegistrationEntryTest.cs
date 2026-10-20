using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Player;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class GearRotationRegistrationEntryTest : GraphicsQualityTestScope
    {
        [Test]
        public void RegistrationStartAndManagerTickReachExistingGearTargets()
        {
            var playerRoot = Own(new GameObject("player"));
            playerRoot.SetActive(false);
            var player = playerRoot.AddComponent<PlayerObjectController>();
            var container = playerRoot.AddComponent<PlayerSystemContainer>();
            SetField(container, "playerObjectController", player);
            var store = Own(new GameObject("blocks")).AddComponent<BlockGameObjectDataStore>();
            var blockRoot = Own(new GameObject("gear block"));
            var block = blockRoot.AddComponent<BlockGameObject>();
            var processor = blockRoot.AddComponent<GearStateChangeProcessor>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).SetValue(block,
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            ((Dictionary<Vector3Int, BlockGameObject>)GetField(store, "_blockObjectsDictionary")).Add(Vector3Int.zero, block);
            var manager = new GearRotationCullingManager(container);
            new GearRotationCullingRegisterService(store, manager).Start();

            // 登録サービスとTickのどちらを消しても停止アサートが落ちる
            // Removing either registration or Tick must fail the stopping assertion
            player.transform.position = new Vector3(200, 0, 0);
            manager.Tick();
            Assert.IsFalse(processor.enabled);
            player.transform.position = Vector3.zero;
            manager.Tick();
            Assert.IsTrue(processor.enabled);
        }
    }
}
