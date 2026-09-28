using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using UnityEngine;

namespace Client.Network.API.Identity
{
    internal sealed class InitialHandshakeClient
    {
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;

        internal InitialHandshakeClient(IItemStackLevelUnlocker itemStackLevelUnlocker)
        {
            _itemStackLevelUnlocker = itemStackLevelUnlocker;
        }

        internal async UniTask<InitialHandshakeAttempt> RunAsync(VanillaApiWithResponse api, PacketExchangeManager packetExchangeManager, string playerIdentity, CancellationToken ct)
        {
            // 身元を送り、サーバーの採番を受け取る
            // Send identity and receive the server-assigned player ID
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerIdentity);
            var initialHandShake = await packetExchangeManager.GetPacketResponse<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(request, ct);

            // 拒否応答からは初期データ取得へ進まない
            // Do not fetch initial data after a rejected handshake
            if (initialHandShake.Rejection != HandshakeRejection.None)
            {
                return new InitialHandshakeAttempt(null, CreateRefusal(initialHandShake.Rejection));
            }
            if (initialHandShake.Accepted == null)
            {
                const string reason = "ハンドシェイクが成功を返しましたが受理データがありません";
                Debug.LogError(reason);
                return new InitialHandshakeAttempt(null, new PlayerStartRefusal(LocalizationKeys.Ui.Loading.HandshakeProtocolError, reason));
            }

            // ハンドシェイクに同梱されたスタックレベルを先に適用（インベントリ等のItemStack生成前に上限を正すため）
            // Apply stack levels bundled in the handshake first so ItemStacks built from later responses use correct limits
            foreach (var itemStackLevel in initialHandShake.Accepted.ItemStackLevels)
            {
                _itemStackLevelUnlocker.UnlockStackLevel(itemStackLevel.ItemGuid, itemStackLevel.Level);
            }

            //必要なデータを取得する
            // Fetch all required resources
            var responses = await UniTask.WhenAll(
                api.World.GetMapObjectInfo(ct),
                api.World.GetWorldData(ct),
                api.Inventory.GetMyPlayerInventory(ct),
                api.Progression.GetChallengeResponse(ct),
                api.Progression.GetUnlockState(ct),
                api.Progression.GetPlayedSkitIds(ct),
                api.Progression.GetResearchNodeStates(ct),
                api.World.GetMapData(ct));

            return new InitialHandshakeAttempt(new InitialHandshakeResponse(initialHandShake, responses), null);
        }

        private static PlayerStartRefusal CreateRefusal(HandshakeRejection rejection)
        {
            var reason = $"ハンドシェイクが拒否されました: {rejection}";
            var key = rejection switch
            {
                HandshakeRejection.AlreadyConnected => LocalizationKeys.Ui.Loading.PlayerAlreadyConnected,
                HandshakeRejection.InvalidIdentity => LocalizationKeys.Ui.Loading.InitializationFailed,
                HandshakeRejection.ConnectionClosed => LocalizationKeys.Ui.Loading.InitializationFailed,
                _ => UnknownRejection(),
            };
            return new PlayerStartRefusal(key, reason);

            #region Internal

            LocalizationKey UnknownRejection()
            {
                Debug.LogError($"未知のハンドシェイク拒否コードを受信しました: {(int)rejection}");
                return LocalizationKeys.Ui.Loading.HandshakeProtocolError;
            }

            #endregion
        }
    }
}
