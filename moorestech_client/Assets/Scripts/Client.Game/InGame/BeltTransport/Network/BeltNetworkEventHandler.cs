using System;
using Client.Network.API;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine;
using VContainer.Unity;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltNetworkEventHandler : IInitializable
    {
        public BeltClientReplica Replica { get; }
        private Exception _failure;
        private readonly UniTaskCompletionSource _failed = new();
        internal UniTask WaitForFailureAsync() => _failed.Task;
        public BeltNetworkEventHandler(InitialHandshakeResponse initial, IVanillaApiEvent events)
        {
            Replica = new BeltClientReplica(initial.BeltSnapshot.ToCore());
            // DI構築時に購読し、finalizerが既存バッファを流す前に受信口を備える。
            // Subscribe during DI construction before the finalizer drains the existing event buffer.
            events.SubscribeEventResponse(BeltTickCompletedEventPacket.EventTag, Receive);
            #region Internal
            void Receive(byte[] payload)
            {
                if (_failure != null)
                {
                    Debug.LogError("Belt tick rejected because synchronization has already failed.");
                    return;
                }
                // 外部入力の復号・検証を完了してから内部の再現処理を呼ぶ。
                // Complete external decoding and validation before invoking internal replay.
                var decoded = BeltTickDecoder.Decode(payload);
                if (!decoded.Succeeded)
                {
                    _failure = new InvalidOperationException(decoded.FailureReason);
                    _failed.TrySetResult();
                    Debug.LogError($"Belt transport packet failed: {decoded.FailureReason}");
                    return;
                }
                Replica.Receive(decoded.Difference);
            }
            #endregion
        }
        public void Initialize() { }
        public void ThrowIfFailed()
        {
            if (_failure != null) throw new InvalidOperationException("Belt transport synchronization failed.", _failure);
        }

    }
}
