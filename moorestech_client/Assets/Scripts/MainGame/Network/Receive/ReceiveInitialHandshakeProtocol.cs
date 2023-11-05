using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Receive
{
    public class ReceiveInitialHandshakeProtocol : IAnalysisPacket
    {
        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(packet.ToArray());

            var position = new Vector2(data.PlayerPos.X, data.PlayerPos.Y);

            InvokeOnFinishHandshakeAsync(position).Forget();
        }

        public event Action<Vector2> OnFinishHandshake;


        private async UniTask InvokeOnFinishHandshakeAsync(Vector2 position)
        {
            await UniTask.SwitchToMainThread();
            OnFinishHandshake?.Invoke(position);
        }
    }
}