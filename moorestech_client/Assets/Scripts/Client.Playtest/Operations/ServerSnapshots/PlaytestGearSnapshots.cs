using System;
using Client.Playtest.Core;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.Gear.Common;
using UnityEngine;

namespace Client.Playtest.Operations
{
    public static class PlaytestGearSnapshots
    {
        // 同じtickの所属を一括複製し、網そのものは渡さない
        // Copy all memberships from one tick without returning mutable networks
        public static async UniTask<GearNetworkId?[]> ReadGearNetworks(this PlaytestDriver driver, IBlock[] blocks)
        {
            var ids = new GearNetworkId?[blocks.Length];
            await PlaytestServerThreadAction.RunAsync("歯車網の所属を複製する", () =>
            {
                var store = ServerContext.GetService<GearNetworkDatastore>();
                for (var i = 0; i < blocks.Length; i++)
                    if (blocks[i] != null && store.TryGetGearNetwork(blocks[i].BlockInstanceId, out var network)) ids[i] = network.NetworkId;
            });
            return ids;
        }

        public static async UniTask WaitForConnectedGears(this PlaytestDriver driver, IBlock first, IBlock second, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                var ids = await driver.ReadGearNetworks(new[] { first, second });
                if (ids[0].HasValue && ids[0].Equals(ids[1])) return;
                if (deadline < Time.realtimeSinceStartup) throw new TimeoutException("歯車網の接続待ちが時間切れです");
                await UniTask.Yield();
            }
        }
    }
}
