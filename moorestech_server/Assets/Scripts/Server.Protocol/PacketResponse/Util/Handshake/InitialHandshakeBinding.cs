using Game.PlayerConnection;
using Game.PlayerIdentity;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Protocol.PacketResponse.Handshake;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Handshake
{
    internal sealed class InitialHandshakeBinding
    {
        private readonly IPlayerIdentityLookup _identityLookup;
        private readonly IPlayerIdentityMutation _identityMutation;
        private readonly PlayerConnectionRegistry _connections;
        private readonly EventProtocolProvider _events;

        internal InitialHandshakeBinding(ServiceProvider provider)
        {
            _identityLookup = provider.GetRequiredService<IPlayerIdentityLookup>();
            _identityMutation = provider.GetRequiredService<IPlayerIdentityMutation>();
            _connections = provider.GetRequiredService<PlayerConnectionRegistry>();
            _events = provider.GetRequiredService<EventProtocolProvider>();
        }

        internal HandshakeRejection Bind(string identity, PacketResponseContext context, out int playerId)
        {
            playerId = 0;
            // 外部入力を検証し、同じ接続の別人への付け替えも拒否する
            // Validate external input and reject switching the identity of an existing connection
            if (!PlayerIdentityText.IsValid(identity, out var reason))
            {
                Debug.LogWarning($"[InitialHandshake] 身元が不正なため拒否: {reason}");
                return HandshakeRejection.InvalidIdentity;
            }
            if (context.PlayerId.HasValue)
            {
                Debug.LogWarning("[InitialHandshake] 接続中の接続に対する再ハンドシェイクを拒否");
                return HandshakeRejection.AlreadyHandshaked;
            }

            // 採番前に既存接続を確認し、その接続のイベント宛先を保護する
            // Check existing connections before assignment to preserve their event destination
            var assignment = _identityLookup.PreviewAssignment(identity);
            if (assignment.Kind == PlayerIdAssignmentKind.Known && _connections.IsConnected(assignment.PlayerId))
            {
                Debug.LogWarning($"[InitialHandshake] 身元{identity}(プレイヤー{assignment.PlayerId})は接続中のため後からの接続を拒否");
                return HandshakeRejection.AlreadyConnected;
            }
            playerId = assignment.PlayerId;

            // 切断処理はバインド直後から解除できるため、その前に登録を揃える
            // Cleanup can unregister immediately after binding, so install both registrations first
            if (!PlayerConnectionBinding.TryBind(playerId, context, _connections, _events))
            {
                Debug.LogWarning($"[InitialHandshake] ハンドシェイク中に切断されたためプレイヤー{playerId}の接続を拒否");
                return HandshakeRejection.ConnectionClosed;
            }

            // ハンドシェイクはメインスレッドで直列処理されるため候補は確定まで変わらない
            // Handshakes run serially on the main thread, keeping the preview stable until assignment
            _identityMutation.Commit(assignment);
            return HandshakeRejection.None;
        }
    }
}
