using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.UnlockState;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ホットバー割当・接続ツール解放の操作群
    ///     Hotbar-assignment and connect-tool-unlock helpers: assigns a placement-target-id reference and waits for the server echo
    /// </summary>
    public static class PlaytestHotbarOps
    {
        // 数字キーのタップと遷移待ちは常に対で使うため1操作に閉じる。片方だけ書いた取りこぼしを構造的に無くす
        // The digit-key tap and the transition wait are always used as a pair, so they close into one operation
        public static async UniTask TapSlotAndWaitUiState(int slot, UIStateEnum expected, float timeoutSeconds)
        {
            await SemanticInput.TapKey(Key.Digit1 + slot);
            await PlaytestUiOps.WaitUiState(expected, timeoutSeconds);
        }

        public static async UniTask AssignHotbar(int slot, string targetName, float timeoutSeconds)
        {
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var datastore = resolver.Resolve<ClientHotbarDatastore>();

            // 供給源はビルドメニューと同一のResolverだが、ロケール非依存のマスタ表示名一致はテスト側で行う
            // The supply source is the same resolver the build menu uses, but the locale-independent master-name match lives here
            var targetId = ResolvePlacementTargetId();

            // 楽観更新はせず、va:event:hotbarUpdateのエコーが戻るまで待つ
            // No optimistic update; wait for the va:event:hotbarUpdate echo to land
            datastore.RequestAssign(slot, targetId);

            var startTime = Time.realtimeSinceStartup;
            while (datastore.Assignments[slot] != targetId)
            {
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"hotbar assign '{targetName}' to slot {slot} not reflected within {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }

            #region Internal

            Guid ResolvePlacementTargetId()
            {
                foreach (var entry in resolver.Resolve<PlacementTargetResolver>().UnlockedEntries())
                {
                    if (entry.MasterDisplayName == targetName) return entry.Id;
                }

                throw new ArgumentException($"Placement target not found (locked or nonexistent): {targetName}");
            }

            #endregion
        }

        public static void UnlockConnectToolServerSide(string toolName)
        {
            // 接続ツールはBlockUnlockStateInfosと別枠(ConnectToolUnlockStateInfos)のため、ブロックのアンロックとは独立に必要
            // Connect tools live in a separate unlock bucket (ConnectToolUnlockStateInfos), so this is required independently of block unlocks
            var connectToolGuid = ResolveConnectToolGuid();
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockConnectTool(connectToolGuid);

            #region Internal

            Guid ResolveConnectToolGuid()
            {
                foreach (var connectTool in MasterHolder.ConnectToolMaster.All)
                {
                    if (connectTool.Name == toolName) return connectTool.ConnectToolGuid;
                }
                throw new ArgumentException($"Connect tool not found: {toolName}");
            }

            #endregion
        }

        public static void UnlockBlueprintServerSide()
        {
            // ブループリントはBlockUnlockStateInfosと別枠(単一bool)のため、接続ツール同様に独立して解放が必要
            // Blueprint lives in a separate unlock bucket (a single bool), so this is required independently, like connect tools
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockBlueprint();
        }
    }
}
