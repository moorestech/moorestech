using System.Collections;
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
                PlaceBlock("直線歯車ベルトコンベア", mergePosition + Vector3Int.back, BlockDirection.North);
                var merge = PlaceBlock("直線歯車ベルトコンベア", mergePosition, BlockDirection.North);
                var source = PlaceBlock("量子チェスト", sourcePosition, BlockDirection.East);
                Assert.IsNotNull(source);
                Assert.IsNotNull(merge);
                transport.RebuildIfDirty();
                var internalLayout = transport.Assembly.Layouts.Single(layout => layout.IsInternal &&
                    layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == source.BlockInstanceId);
                var mergeLayout = transport.Assembly.Layouts[internalLayout.Outputs[0].PartnerSegmentIndex];
                Assert.AreEqual(2, mergeLayout.Inputs.Length);
                var segment = (BeltBufferedSegment)transport.Assembly.Segments[mergeLayout.Index];

                // 合流を詰まらせ非同期応答中も内部の対象を保持する
                // Block the merge so the target stays internal while awaiting API responses
                segment.RestoreItems(new[] { new BeltItemState(new BeltItem(filler, ItemInstanceId.Create(), BeltEntryDirection.FromBack), 0) });
                segment.Buffer.RestoreItem(new BeltItem(filler, ItemInstanceId.Create(), BeltEntryDirection.FromBack));
                var item = new BeltItem(itemId, ItemInstanceId.Create(), BeltEntryDirection.FromLeft);
                transport.Assembly.Segments[internalLayout.Index].RestoreItems(new[] { new BeltItemState(item, 100) });
                Assert.AreEqual(item.ItemInstanceId, transport.GetConnectionRemovalRefundItems(source).Single().ItemInstanceId);
                for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                    inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(filler, 1));
                var before = inventory.InventoryItems.ToArray();
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] before carried={Carried()} refunded={Count()} tick={GameUpdater.CurrentTick}");

                // 満杯では撤去せず対象個体と手持ちを保つ
                // A full inventory preserves both the source item and player inventory
                var denied = await ClientContext.VanillaApi.Response.Block.BlockRemove(sourcePosition, CancellationToken.None);
                Assert.IsFalse(denied.Success);
                Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, denied.FailureReason);
                Assert.AreSame(source, ServerContext.WorldBlockDatastore.GetBlock(sourcePosition));
                CollectionAssert.AreEqual(before, inventory.InventoryItems);
                Assert.AreEqual(1, Carried());
                Assert.AreEqual(item.ItemInstanceId, transport.GetConnectionRemovalRefundItems(source).Single().ItemInstanceId);
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] denied={denied.FailureReason} carried={Carried()} refunded={Count()} tick={GameUpdater.CurrentTick}");
                for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                    inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());

                // 本番APIの再試行で一度だけ返却する
                // Retry through the production API and refund exactly once
                var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(sourcePosition, CancellationToken.None);
                Assert.IsTrue(response.Success, response.FailureReason.ToString());
                Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(sourcePosition));
                Assert.AreEqual(1, Count());
                var tick = GameUpdater.CurrentTick;
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] removed success={response.Success} carried={Carried()} refunded={Count()} tick={tick}");

                // 次tickの再構築後も対象品の総量を検証
                // Verify target-item conservation after the next real tick rebuild
                for (var frame = 0; frame < 300 && GameUpdater.CurrentTick == tick; frame++) await UniTask.Yield();
                Assert.Greater(GameUpdater.CurrentTick, tick, "The running server must advance a real tick.");
                Assert.AreEqual(0, Carried());
                Assert.AreEqual(1, Count());
                Assert.IsFalse(transport.Assembly.Layouts.Any(layout => layout.IsInternal &&
                    layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == source.BlockInstanceId));
                Debug.Log($"[BeltConnectionRemovalRefundPlaying] after-tick carried={Carried()} refunded={Count()} tick={GameUpdater.CurrentTick}");

                int Count()
                {
                    return inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
                }

                int Carried()
                {
                    return transport.Assembly.Segments.Sum(entry => entry.CaptureItems().Count(state => state.Item.ItemId == itemId) +
                        (entry is BeltBufferedSegment buffered && buffered.Buffer.TryGetItem(out var held) && held.ItemId == itemId ? 1 : 0));
                }
            }

            #endregion
        }
    }
}
