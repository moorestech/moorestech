using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using UnityEngine;

namespace Client.Network.API
{
    internal static class InitialHandshakeClient
    {
        public static async UniTask<InitialHandshakeResponse> RunAsync(VanillaApiWithResponse api, PacketExchangeManager packetExchangeManager, string playerIdentity, CancellationToken ct)
        {
            // 身元を送り、サーバーの採番を受け取る
            // Send identity and receive the server-assigned player ID
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerIdentity);
            var initialHandShake = await packetExchangeManager.GetPacketResponse<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(request, ct);

            // 拒否応答からは初期データ取得へ進まない
            // Do not fetch initial data after a rejected handshake
            if (initialHandShake.Rejection != HandshakeRejection.None)
            {
                Debug.LogWarning($"Handshake rejected: {initialHandShake.Rejection}");
                throw new PlayerHandshakeRejectedException(initialHandShake.Rejection);
            }

            // ハンドシェイクに同梱されたスタックレベルを先に適用（インベントリ等のItemStack生成前に上限を正すため）
            // Apply stack levels bundled in the handshake first so ItemStacks built from later responses use correct limits
            var itemStackLevelUnlocker = ServerContext.GetService<IItemStackLevelUnlocker>();
            foreach (var itemStackLevel in initialHandShake.ItemStackLevels)
            {
                itemStackLevelUnlocker.UnlockStackLevel(itemStackLevel.ItemGuid, itemStackLevel.Level);
            }

            //必要なデータを取得する
            // Fetch all required resources
            var responses = await UniTask.WhenAll(
                api.GetMapObjectInfo(ct),
                api.GetWorldData(ct),
                api.GetMyPlayerInventory(ct),
                api.GetChallengeResponse(ct),
                api.GetUnlockState(ct),
                api.GetPlayedSkitIds(ct),
                api.GetResearchNodeStates(ct),
                api.GetMapData(ct));

            return new InitialHandshakeResponse(initialHandShake, responses);
        }

    }
}
