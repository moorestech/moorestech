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
                // 一時ワールドでBPを正規解放し、空いているセルにコピー対象を置く
                // Unlock blueprints in the temporary world and place a copy target in an empty cell
                var unlock = ServerContext.GetService<IGameUnlockStateDataController>();
                unlock.UnlockBlueprint();
                Assert.IsTrue(unlock.IsBlueprintUnlocked);
                Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
                placedBlock = PlaceBlock("量子チェスト", position, BlockDirection.North);
                Assert.IsNotNull(placedBlock);
                await WaitBlockGameObjectSpawn(position);

                // 実コピー系の公開遷移で、範囲確定後の名前入力局面を作る
                // Use the real copy system's public transitions to reach naming after range selection
                selection.SelectStart(placedBlock.BlockPositionInfo.OriginalPos);
                selection.SelectEnd(placedBlock.BlockPositionInfo.OriginalPos);
                Assert.AreEqual(BlueprintCopyPhase.AwaitingName, selection.Phase);
                nameInput.Open();
                Assert.IsNotNull(modal.Pending, "BP名入力を開いても入力モーダルが保留されない");
                Assert.IsTrue(modal.Pending.RequiresInput);
                Assert.IsTrue(modal.Respond(modal.Pending.Id, "confirm", blueprintName));

                // 名前通知だけで終えず、実コピー応答と非同期サムネイル撮影を待つ
                // Wait beyond the name notification for the real copy response and asynchronous thumbnail capture
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
                    // 固有名で作ったBPだけを削除し、キャッシュと撮影結果の除去まで待つ
                    // Delete only this uniquely named blueprint and wait until cache and thumbnail removal finish
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
                    // 前提検証に失敗しても、自分で設置したブロックだけを取り除く
                    // Remove only the block placed by this probe even when a precondition fails
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
