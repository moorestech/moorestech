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
    public class BeltConnectionRemovalRefundPlayingTest
    {
        [UnityTest]
        public IEnumerator RunningGameRefundsSourceInternalItemAfterFullInventoryRetry()
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
                var mergePosition = new Vector3Int(100, 100, 101);
                var sourcePosition = mergePosition + Vector3Int.left;

                // 固定テストマスタのチェストから合流へ接続する
                // Connect the chest from the fixed test master data to a merge
                var item = new BeltItem(itemId, ItemInstanceId.Create(), BeltEntryDirection.FromLeft);
                var placement = new PlaceSourceIntoBlockedMerge(mergePosition, sourcePosition, item, filler, transport);
                await placement.RunOnServerAsync();
                var source = placement.Source;
                Assert.IsNotNull(source);
                Assert.IsNotNull(placement.Merge);
                Assert.AreEqual(1, placement.MatchedInternalLayoutCount);
                Assert.AreEqual(2, placement.MergeInputCount);
                Assert.AreEqual(item.ItemInstanceId, placement.Refunds.Single().ItemInstanceId);
                for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                    inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(filler, 1));
                var before = inventory.InventoryItems.ToArray();
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] before carried={(await ReadTransport(false)).Carried} refunded={Count()} tick={GameUpdater.CurrentTick}");

                // 満杯では撤去せず対象個体と手持ちを保つ
                // A full inventory preserves both the source item and player inventory
                var denied = await ClientContext.VanillaApi.Response.Block.BlockRemove(sourcePosition, CancellationToken.None);
                Assert.IsFalse(denied.Success);
                Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, denied.FailureReason);
                Assert.AreSame(source, ServerContext.WorldBlockDatastore.GetBlock(sourcePosition));
                CollectionAssert.AreEqual(before, inventory.InventoryItems);
                var afterDenied = await ReadTransport(true);
                Assert.AreEqual(1, afterDenied.Carried);
                Assert.AreEqual(item.ItemInstanceId, afterDenied.Refunds.Single().ItemInstanceId);
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] denied={denied.FailureReason} carried={afterDenied.Carried} refunded={Count()} tick={GameUpdater.CurrentTick}");
                for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                    inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());

                // 本番APIの再試行で一度だけ返却する
                // Retry through the production API and refund exactly once
                var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(sourcePosition, CancellationToken.None);
                Assert.IsTrue(response.Success, response.FailureReason.ToString());
                Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(sourcePosition));
                Assert.AreEqual(1, Count());
                var tick = GameUpdater.CurrentTick;
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] removed success={response.Success} carried={(await ReadTransport(false)).Carried} refunded={Count()} tick={tick}");

                // 次tickの再構築後も対象品の総量を検証
                // Verify target-item conservation after the next real tick rebuild
                for (var frame = 0; frame < 300 && GameUpdater.CurrentTick == tick; frame++) await UniTask.Yield();
                Assert.Greater(GameUpdater.CurrentTick, tick, "The running server must advance a real tick.");
                var afterTick = await ReadTransport(false);
                Assert.AreEqual(0, afterTick.Carried);
                Assert.AreEqual(1, Count());
                Assert.IsFalse(afterTick.HasSourceInternalLayout);
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] after-tick carried={afterTick.Carried} refunded={Count()} tick={GameUpdater.CurrentTick}");

                int Count()
                {
                    return inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
                }

                async UniTask<ReadTransportStep> ReadTransport(bool readRefunds)
                {
                    var read = new ReadTransportStep(itemId, source, readRefunds, transport);
                    await read.RunOnServerAsync();
                    return read;
                }
            }

            #endregion
        }

        // 合流へ繋がるチェストを置いて網を作り直し、合流を詰まらせて内部segmentに対象品を載せる
        // Places a chest feeding a merge, rebuilds the network, blocks the merge and loads the target into the internal segment
        private sealed class PlaceSourceIntoBlockedMerge : ServerTickEndTestStep
        {
            private readonly Vector3Int _mergePosition;
            private readonly Vector3Int _sourcePosition;
            private readonly BeltItem _item;
            private readonly ItemId _filler;
            private readonly BeltTransportDatastore _transport;

            public IBlock Source { get; private set; }
            public IBlock Merge { get; private set; }
            public int MatchedInternalLayoutCount { get; private set; }
            public int MergeInputCount { get; private set; }
            public IReadOnlyList<IItemStack> Refunds { get; private set; }

            public PlaceSourceIntoBlockedMerge(Vector3Int mergePosition, Vector3Int sourcePosition, BeltItem item, ItemId filler, BeltTransportDatastore transport)
            {
                _mergePosition = mergePosition;
                _sourcePosition = sourcePosition;
                _item = item;
                _filler = filler;
                _transport = transport;
            }

            protected override void Execute()
            {
                PlaceBlock("直線歯車ベルトコンベア", _mergePosition + Vector3Int.back, BlockDirection.North);
                Merge = PlaceBlock("直線歯車ベルトコンベア", _mergePosition, BlockDirection.North);
                Source = PlaceBlock("量子チェスト", _sourcePosition, BlockDirection.East);
                if (Source == null || Merge == null) return;
                _transport.RebuildIfDirty();

                // 一致数はメインスレッドで検証する。ここで例外を投げると更新ループに握られて待ちが打ち切りまで続く
                // The match count is asserted on the main thread; throwing here would be swallowed by the update loop and stall the wait
                var internalLayouts = _transport.Assembly.Layouts.Where(layout => layout.IsInternal &&
                    layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == Source.BlockInstanceId).ToArray();
                MatchedInternalLayoutCount = internalLayouts.Length;
                if (internalLayouts.Length != 1) return;
                var mergeLayout = _transport.Assembly.Layouts[internalLayouts[0].Outputs[0].PartnerSegmentIndex];
                MergeInputCount = mergeLayout.Inputs.Length;
                if (!(_transport.Assembly.Segments[mergeLayout.Index] is BeltBufferedSegment segment)) return;

                // 合流を詰まらせ非同期応答中も内部の対象を保持する
                // Block the merge so the target stays internal while awaiting API responses
                segment.RestoreItems(new[] { new BeltItemState(new BeltItem(_filler, ItemInstanceId.Create(), BeltEntryDirection.FromBack), 0) });
                segment.Buffer.RestoreItem(new BeltItem(_filler, ItemInstanceId.Create(), BeltEntryDirection.FromBack));
                _transport.Assembly.Segments[internalLayouts[0].Index].RestoreItems(new[] { new BeltItemState(_item, 100) });
                Refunds = _transport.GetConnectionRemovalRefundItems(Source);
            }
        }

        // 搬送tickと重ならない時点で、対象品の搬送中の数・返却物・チェスト由来の内部segmentの有無を読む
        // Reads the carried target count, the refunds and whether the chest's internal segment exists at a point that never overlaps the transport tick
        private sealed class ReadTransportStep : ServerTickEndTestStep
        {
            private readonly ItemId _itemId;
            private readonly IBlock _source;
            private readonly bool _readRefunds;
            private readonly BeltTransportDatastore _transport;

            public int Carried { get; private set; }
            public IReadOnlyList<IItemStack> Refunds { get; private set; }
            public bool HasSourceInternalLayout { get; private set; }

            public ReadTransportStep(ItemId itemId, IBlock source, bool readRefunds, BeltTransportDatastore transport)
            {
                _itemId = itemId;
                _source = source;
                _readRefunds = readRefunds;
                _transport = transport;
            }

            protected override void Execute()
            {
                Carried = _transport.Assembly.Segments.Sum(entry => entry.CaptureItems().Count(state => state.Item.ItemId == _itemId) +
                    (entry is BeltBufferedSegment buffered && buffered.Buffer.TryGetItem(out var held) && held.ItemId == _itemId ? 1 : 0));
                HasSourceInternalLayout = _transport.Assembly.Layouts.Any(layout => layout.IsInternal &&
                    layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == _source.BlockInstanceId);
                if (_readRefunds) Refunds = _transport.GetConnectionRemovalRefundItems(_source);
            }
        }
    }
}
