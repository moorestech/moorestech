using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Receive
{
    public class ReceiveInitialHandshakeProtocol: IAnalysisPacket
    {
        public event Action<Vector2> OnFinishHandshake;
        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(packet.ToArray());
            
            var position = new Vector2(data.PlayerPos.X,data.PlayerPos.Y);

            InvokeOnFinishHandshakeAsync(position).Forget();
            
        }
        
      
        private async UniTask InvokeOnFinishHandshakeAsync(Vector2 position)
        {
            await UniTask.SwitchToMainThread();
            OnFinishHandshake?.Invoke(position);
        }
    }
}