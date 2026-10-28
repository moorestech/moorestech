using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Client.Tests.EditModeInPlayingTest.Util;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class BeltRemovalRefundPlayingTest
    {
        [UnityTest]
        public IEnumerator RunningGameRefundsBeltsAndRetriesFullInventory()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
                var transport = ServerContext.GetService<BeltTransportDatastore>();
                var itemId = MasterHolder.ItemMaster.GetItemAllIds().Single(id => MasterHolder.ItemMaster.GetItemMaster(id).Name == "亜鉛鉱石");
                var filler = MasterHolder.ItemMaster.GetItemAllIds().Single(id => MasterHolder.ItemMaster.GetItemMaster(id).Name == "小石");
                Debug.Log($"[BeltRemovalRefundPlaying] booted player={playerId} tick={GameUpdater.CurrentTick}");

                // 通常・歯車の実マスタで本番経路を検証
                // Verify the production path with real normal and gear belt masters
                var testCases = new (string BlockName, bool VerifyFullInventoryRetry)[]
                {
                    ("直進高速ベルトコンベア", false),
                    ("直線歯車ベルトコンベア", true),
                };
                for (var index = 0; index < testCases.Length; index++)
                {
                    var testCase = testCases[index];
                    ClearInventory();
                    var position = new Vector3Int(100 + index * 3, 100, 100);
                    var item = new BeltItem(itemId, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
                    var placement = new PlaceBeltCarryingItem(testCase.BlockName, position, item, transport);
                    await placement.RunOnServerAsync();
                    var block = placement.Block;
                    Assert.IsNotNull(block);
                    Assert.AreEqual(1, placement.MatchedLayoutCount);
                    Assert.AreEqual(item.ItemInstanceId, placement.Refunds.Single().ItemInstanceId);
                    Assert.AreEqual(0, Count());

                    // 歯車では満杯拒否後に同じ撤去を再試行
                    // Retry the same gear belt removal after a full-inventory rejection
                    if (testCase.VerifyFullInventoryRetry)
                    {
                        for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                            inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(filler, 1));
                        var before = inventory.InventoryItems.ToArray();
                        var denied = await ClientContext.VanillaApi.Response.Block.BlockRemove(position, CancellationToken.None);
                        Assert.IsFalse(denied.Success);
                        Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, denied.FailureReason);
                        Assert.AreSame(block, ServerContext.WorldBlockDatastore.GetBlock(position));
                        CollectionAssert.AreEqual(before, inventory.InventoryItems);
                        Assert.AreEqual(1, await ReadOwnedRefundCount(block));
                        Debug.Log($"[BeltRemovalRefundPlaying] denied={denied.FailureReason} carried=1 refunded={Count()} tick={GameUpdater.CurrentTick}");
                        ClearInventory();
                    }

                    // API応答後も実tickを待ち、再構築による復活を検知
                    // Wait for a real tick after the API response to detect rebuild resurrection
                    var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(position, CancellationToken.None);
                    Assert.IsTrue(response.Success, response.FailureReason.ToString());
                    Assert.AreEqual(1, Count());
                    Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(position));
                    var tick = GameUpdater.CurrentTick;
                    Debug.Log($"[BeltRemovalRefundPlaying] removed={testCase.BlockName} success={response.Success} refunded={Count()} tick={tick}");
                    for (var frame = 0; frame < 300 && GameUpdater.CurrentTick == tick; frame++) await UniTask.Yield();
                    Assert.Greater(GameUpdater.CurrentTick, tick, "The running server must advance a real tick.");
                    Assert.AreEqual(0, await ReadOwnedRefundCount(block));
                    Assert.AreEqual(1, Count());
                    Debug.Log($"[BeltRemovalRefundPlaying] after-tick carried=0 refunded={Count()} tick={GameUpdater.CurrentTick}");
                }

                async UniTask<int> ReadOwnedRefundCount(IBlock target)
                {
                    var read = new ReadOwnedRefundCountStep(target.BlockInstanceId, transport);
                    await read.RunOnServerAsync();
                    return read.Count;
                }

                int Count()
                {
                    return inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
                }

                void ClearInventory()
                {
                    for (var slot = 0; slot < inventory.GetSlotSize(); slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
                }
            }

            #endregion
        }

        // ベルトを置いて網を作り直し、搬送中アイテムを1つ載せて返却物を読む
        // Places a belt, rebuilds the network, loads one carried item and reads the refunds
        private sealed class PlaceBeltCarryingItem : ServerTickEndTestStep
        {
            private readonly string _blockName;
            private readonly Vector3Int _position;
            private readonly BeltItem _item;
            private readonly BeltTransportDatastore _transport;

            public IBlock Block { get; private set; }
            public int MatchedLayoutCount { get; private set; }
            public IReadOnlyList<IItemStack> Refunds { get; private set; }

            public PlaceBeltCarryingItem(string blockName, Vector3Int position, BeltItem item, BeltTransportDatastore transport)
            {
                _blockName = blockName;
                _position = position;
                _item = item;
                _transport = transport;
            }

            protected override void Execute()
            {
                Block = PlaceBlock(_blockName, _position, BlockDirection.North);
                _transport.RebuildIfDirty();

                // 一致数はメインスレッドで検証する。ここで例外を投げると更新ループに握られて待ちが打ち切りまで続く
                // The match count is asserted on the main thread; throwing here would be swallowed by the update loop and stall the wait
                var layouts = _transport.Assembly.Layouts.Where(entry => entry.Cells.Any(cell => cell.Position == _position)).ToArray();
                MatchedLayoutCount = layouts.Length;
                if (layouts.Length != 1) return;
                _transport.Assembly.Segments[layouts[0].Index].RestoreItems(new[] { new BeltItemState(_item, 100) });
                Refunds = _transport.GetOwnedRefundItems(Block.BlockInstanceId);
            }
        }

        // 搬送tickと重ならない時点でblockの返却物を数える
        // Counts a block's refunds at a point that never overlaps the transport tick
        private sealed class ReadOwnedRefundCountStep : ServerTickEndTestStep
        {
            private readonly BlockInstanceId _blockInstanceId;
            private readonly BeltTransportDatastore _transport;

            public int Count { get; private set; }

            public ReadOwnedRefundCountStep(BlockInstanceId blockInstanceId, BeltTransportDatastore transport)
            {
                _blockInstanceId = blockInstanceId;
                _transport = transport;
            }

            protected override void Execute()
            {
                Count = _transport.GetOwnedRefundItems(_blockInstanceId).Count;
            }
        }
    }
}
