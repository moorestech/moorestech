using System;
using System.Collections.Generic;
using System.Threading;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Protocol;
using UniRx;
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;

namespace Client.Network.API
{
    public class VanillaApiEvent
    {
        private readonly Dictionary<string, Subject<byte[]>> _eventResponseSubjects = new();
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly PlayerConnectionSetting _playerConnectionSetting;
        
        public VanillaApiEvent(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchangeManager = packetExchangeManager;
            _playerConnectionSetting = playerConnectionSetting;
            CollectEvent().Forget();
        }
        
        private async UniTask CollectEvent()
        {
            while (true)
            {
                var ct = new CancellationTokenSource().Token;
                
                try
                {
                    await RequestAndParse(ct);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Event Protocol Error:{e.Message}\n{e.StackTrace}");
                }
                
                await UniTask.Delay(ServerConst.PollingRateMillSec, cancellationToken: ct);
            }
            
            #region Internal
            
            async UniTask RequestAndParse(CancellationToken ct)
            {
                var request = new EventProtocolMessagePack(_playerConnectionSetting.PlayerId);
                
                var response = await _packetExchangeManager.GetPacketResponse<ResponseEventProtocolMessagePack>(request, ct);
                
                foreach (var eventMessagePack in response.Events)
                {
                    if (!_eventResponseSubjects.TryGetValue(eventMessagePack.Tag, out var subjects)) continue;
                    
                    subjects.OnNext(eventMessagePack.Payload);
                }
            }
            
            #endregion
        }
        
        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            if (!_eventResponseSubjects.TryGetValue(tag, out var subject))
            {
                subject = new Subject<byte[]>();
                _eventResponseSubjects.Add(tag, subject);
            }
            
            return subject.Subscribe(responseAction);
        }
    }
}