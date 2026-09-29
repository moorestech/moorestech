using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
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

            // タイムアウト・デシリアライズ失敗はnullで返る。そのまま参照すると起動が無言でNREで落ちる
            // A timeout or a failed deserialization returns null; dereferencing it would drop the boot into a silent NRE
            if (initialHandShake == null)
            {
                return Refuse("ハンドシェイク応答が届きませんでした（タイムアウトまたはデシリアライズ失敗）");
            }

            // 拒否応答からは初期データ取得へ進まない
            // Do not fetch initial data after a rejected handshake
            if (initialHandShake.Rejection != HandshakeRejection.None)
            {
                return InitialHandshakeAttempt.Refused(initialHandShake.Rejection, $"ハンドシェイクが拒否されました: {initialHandShake.Rejection}");
            }
            if (initialHandShake.Accepted == null)
            {
                return Refuse("ハンドシェイクが成功を返しましたが受理データがありません");
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

            return InitialHandshakeAttempt.Succeeded(new InitialHandshakeResponse(initialHandShake, responses));

            #region Internal

            // 拒否コードを持たない不整合はNoneで返し、理由だけを上位へ渡す
            // A mismatch with no rejection code returns None and hands only the reason upstairs
            InitialHandshakeAttempt Refuse(string reason)
            {
                Debug.LogError(reason);
                return InitialHandshakeAttempt.Refused(HandshakeRejection.None, reason);
            }

            #endregion
        }
    }
}
