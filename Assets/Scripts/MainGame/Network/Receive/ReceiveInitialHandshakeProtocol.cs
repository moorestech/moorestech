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

            
            Debug.Log("ハンドシェイク1　ThreadId " + Thread.CurrentThread.ManagedThreadId);
            InvokeOnFinishHandshakeAsync(position).Forget();
            Debug.Log("ハンドシェイク2　ThreadId " + Thread.CurrentThread.ManagedThreadId);
            
        }
        
      
        private async UniTask InvokeOnFinishHandshakeAsync(Vector2 position)
        {
            Debug.Log("ハンドシェイク3　ThreadId " + Thread.CurrentThread.ManagedThreadId);
            await UniTask.SwitchToMainThread();
            OnFinishHandshake?.Invoke(position);
            
            Debug.Log("ハンドシェイク4　ThreadId " + Thread.CurrentThread.ManagedThreadId);
        }
    }
}