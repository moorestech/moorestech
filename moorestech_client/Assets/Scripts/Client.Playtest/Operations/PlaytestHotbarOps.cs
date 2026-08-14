using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.UnlockState;
using UnityEngine;
using VContainer;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ホットバー割当・接続ツール解放の操作群
    ///     Hotbar-assignment and connect-tool-unlock helpers: assigns a placement-target-id reference and waits for the server echo
    /// </summary>
    public static class PlaytestHotbarOps
    {
        public static async UniTask AssignHotbar(int slot, string targetName, float timeoutSeconds)
        {
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var datastore = resolver.Resolve<ClientHotbarDatastore>();

            // 解決はビルドメニューと同一の PlacementTargetResolver 1本に寄せる。未解放対象は割当できない
            // Resolution goes through the single PlacementTargetResolver the build menu uses; locked targets cannot be assigned
            if (!resolver.Resolve<PlacementTargetResolver>().TryResolveByDisplayName(targetName, out var target))
                throw new ArgumentException($"Placement target not found (locked or nonexistent): {targetName}");

            // 楽観更新はせず、va:event:hotbarUpdateのエコーが戻るまで待つ
            // No optimistic update; wait for the va:event:hotbarUpdate echo to land
            datastore.RequestAssign(slot, target.Id);

            var startTime = Time.realtimeSinceStartup;
            while (datastore.Assignments[slot] != target.Id)
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
    }
}
