using System;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Modal;
using Client.Tests.Common;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 実コピーの前提を作り、Webなしで名前確定から撮影・後片付けまで検証する
    // Prepare a real copy and verify naming, capture and cleanup without the web host
    internal static class ModalStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var modal = resolver.Resolve<ModalRequestService>();
            var nameInput = resolver.Resolve<BlueprintNameInputState>();
            var library = resolver.Resolve<ClientBlueprintLibrary>();
            var thumbnails = resolver.Resolve<BlueprintThumbnailContainer>();
            var selection = TestReflection.GetField<BlueprintCopySelection>(resolver.Resolve<BlueprintCopySystem>(), "_selection");
            var existingIds = library.Blueprints.Select(blueprint => blueprint.BlueprintGuid).ToArray();
            var blueprintName = $"hostless-bp-{Guid.NewGuid():N}";
            var position = new Vector3Int(17, 0, 17);
            IBlock placedBlock = null;
            string confirmedName = null;
            using var confirmSubscription = nameInput.OnConfirm.Subscribe(name => confirmedName = name);

            try
            {
                // BPを解放しコピー対象を置く
                // Unlock BPs and place a copy target
                var unlock = ServerContext.GetService<IGameUnlockStateDataController>();
                unlock.UnlockBlueprint();
                Assert.IsTrue(unlock.IsBlueprintUnlocked);
                Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
                placedBlock = PlaceBlock("量子チェスト", position, BlockDirection.North);
                Assert.IsNotNull(placedBlock);
                await WaitBlockGameObjectSpawn(position);

                // 範囲確定後の名前入力局面
                // Reach naming after range selection
                selection.SelectStart(placedBlock.BlockPositionInfo.OriginalPos);
                selection.SelectEnd(placedBlock.BlockPositionInfo.OriginalPos);
                Assert.AreEqual(BlueprintCopyPhase.AwaitingName, selection.Phase);
                nameInput.Open();
                Assert.IsNotNull(modal.Pending, "BP名入力を開いても入力モーダルが保留されない");
                Assert.IsTrue(modal.Pending.RequiresInput);
                Assert.IsTrue(modal.Respond(modal.Pending.Id, ModalResult.Confirm, blueprintName));

                // コピー応答と撮影を待つ
                // Wait for the copy response and capture
                for (var i = 0; i < 600; i++)
                {
                    var created = library.Blueprints.SingleOrDefault(blueprint => blueprint.Name == blueprintName);
                    if (created != null && selection.Phase == BlueprintCopyPhase.SelectingStart && thumbnails.Contains(created.BlueprintGuid)) break;
                    await UniTask.Yield();
                }
                var completed = library.Blueprints.SingleOrDefault(blueprint => blueprint.Name == blueprintName);
                Assert.AreEqual(blueprintName, confirmedName, "モーダルの確定がBP名入力へ書き戻されない");
                Assert.IsNull(modal.Pending);
                Assert.IsNotNull(completed, "BP作成結果がライブラリに反映されない");
                Assert.AreEqual(BlueprintCopyPhase.SelectingStart, selection.Phase);
                Assert.IsTrue(thumbnails.Contains(completed.BlueprintGuid), "BPサムネイルの撮影が完了しない");
            }
            finally
            {
                try
                {
                    // 固有名のBPだけ削除
                    // Delete only the uniquely named BP
                    var created = library.Blueprints.SingleOrDefault(blueprint => blueprint.Name == blueprintName);
                    if (created != null)
                    {
                        var result = await library.DeleteBlueprint(created.BlueprintGuid, CancellationToken.None);
                        Assert.AreEqual(BlueprintDeleteResult.Success, result);
                        for (var i = 0; i < 600 && (library.Blueprints.Any(blueprint => blueprint.BlueprintGuid == created.BlueprintGuid) || thumbnails.Contains(created.BlueprintGuid)); i++) await UniTask.Yield();
                        Assert.IsFalse(library.Blueprints.Any(blueprint => blueprint.BlueprintGuid == created.BlueprintGuid));
                        Assert.IsFalse(thumbnails.Contains(created.BlueprintGuid));
                    }
                    CollectionAssert.AreEquivalent(existingIds, library.Blueprints.Select(blueprint => blueprint.BlueprintGuid));
                }
                finally
                {
                    // 自設置ブロックだけ除去
                    // Remove only the block we placed
                    if (placedBlock != null)
                    {
                        Assert.IsTrue(RemoveBlock(position));
                        for (var i = 0; i < 600 && ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(position, out _); i++) await UniTask.Yield();
                        Assert.IsFalse(ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(position, out _));
                    }
                }
            }
        }
    }
}
