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

                // 実在する通常・歯車のマスタで同じ本番経路を通す
                // Exercise the same production path with real normal and gear belt masters
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
                    var block = PlaceBlock(testCase.BlockName, position, BlockDirection.North);
                    Assert.IsNotNull(block);
                    transport.RebuildIfDirty();
                    var layout = transport.Assembly.Layouts.Single(entry => entry.Cells.Any(cell => cell.Position == position));
                    var item = new BeltItem(itemId, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
                    transport.Assembly.Segments[layout.Index].RestoreItems(new[] { new BeltItemState(item, 100) });
                    var refund = transport.GetRefundItems(block.BlockInstanceId).Single();
                    Assert.AreEqual(item.ItemInstanceId, refund.ItemInstanceId);
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
                        Assert.AreEqual(1, transport.GetRefundItems(block.BlockInstanceId).Count);
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
                    Assert.IsEmpty(transport.GetRefundItems(block.BlockInstanceId));
                    Assert.AreEqual(1, Count());
                    Debug.Log($"[BeltRemovalRefundPlaying] after-tick carried=0 refunded={Count()} tick={GameUpdater.CurrentTick}");
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
    }
}
