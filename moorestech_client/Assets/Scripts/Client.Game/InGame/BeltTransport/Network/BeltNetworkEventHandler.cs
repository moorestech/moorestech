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
        public UniTask WaitForFailureAsync() => _failed.Task;
        public BeltNetworkEventHandler(InitialHandshakeResponse initial, IVanillaApiEvent events)
        {
            Replica = new BeltClientReplica(initial.BeltSnapshot.ToCore());
            // DI構築時に購読し、finalizerが既存バッファを流す前に受信口を備える。
            // Subscribe during DI construction before the finalizer drains the existing event buffer.
            events.SubscribeEventResponse(BeltTickCompletedEventPacket.EventTag, Receive);
        }
        public void Initialize() { }
        public void ThrowIfFailed()
        {
            if (_failure != null) throw new InvalidOperationException("Belt transport synchronization failed.", _failure);
        }
        private void Receive(byte[] payload)
        {
            if (_failure != null)
            {
                Debug.LogError("Belt tick rejected because synchronization has already failed.");
                return;
            }
            // 外部パケット境界で破損を隔離し、起動待機にも失敗を伝える。
            // Isolate malformed external packets and propagate failure to startup waiting.
            try
            {
                var message = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload);
                Replica.Receive(message.ToCore());
            }
            catch (Exception error)
            {
                _failure = error;
                _failed.TrySetResult();
                Debug.LogError($"Belt transport packet failed: {error}");
            }
        }
    }
}
