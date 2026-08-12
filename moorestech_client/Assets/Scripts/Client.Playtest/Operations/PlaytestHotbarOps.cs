using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Common.Debug;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using UnityEngine;
using VContainer;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ホットバー割当・接続ツールアンロックの操作群。設置対象IDへの参照割当とサーバーエコー待ちを提供
    ///     Hotbar-assignment and connect-tool-unlock helpers: assigns a placement-target-id reference and waits for the server echo
    /// </summary>
    public static class PlaytestHotbarOps
    {
        public static async UniTask AssignHotbar(int slot, string targetName, float timeoutSeconds)
        {
            var datastore = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ClientHotbarDatastore>();
            var targetId = ResolveTargetId(targetName);

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
        }

        public static void UnlockConnectToolServerSide(string toolName)
        {
            // 接続ツールはBlockUnlockStateInfosと別枠(ConnectToolUnlockStateInfos)のため、ブロックのアンロックとは独立に必要
            // Connect tools live in a separate unlock bucket (ConnectToolUnlockStateInfos), so this is required independently of block unlocks
            var connectToolGuid = ResolveConnectToolGuid(toolName);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockConnectTool(connectToolGuid);
        }

        private static Guid ResolveTargetId(string targetName)
        {
            // ビルドメニューと同一供給源(PlacementTargetCatalog.UnlockedEntries)から表示名で解決する。未解放対象は割当できない
            // Resolves by display name from the same supply source as the build menu (PlacementTargetCatalog.UnlockedEntries); locked targets cannot be assigned
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var catalog = resolver.Resolve<PlacementTargetCatalog>();
            var blueprintLibrary = resolver.Resolve<ClientBlueprintLibrary>();
            var unlockState = resolver.Resolve<IGameUnlockStateData>();
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            foreach (var entry in catalog.UnlockedEntries(unlockState, showAllPlaceable, blueprintLibrary.BlueprintEntries))
            {
                if (entry.MasterDisplayName == targetName) return entry.Id;
            }

            throw new ArgumentException($"Placement target not found (locked or nonexistent): {targetName}");
        }

        private static Guid ResolveConnectToolGuid(string toolName)
        {
            foreach (var connectTool in MasterHolder.ConnectToolMaster.All)
            {
                if (connectTool.Name == toolName) return connectTool.ConnectToolGuid;
            }
            throw new ArgumentException($"Connect tool not found: {toolName}");
        }
    }
}
