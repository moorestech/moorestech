using MainGame.Basic.Server;
using MainGame.Network.Receive;
using MainGame.Network.Send;
using MainGame.UnityView.Game;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Player
{
    public class PlayerPositionSender : ITickable
    {
        private readonly SendPlayerPositionProtocolProtocol _protocol;
        private readonly IPlayerPosition _playerPosition;
        

        private bool _startPositionSend = false;
        
        public PlayerPositionSender(SendPlayerPositionProtocolProtocol protocol, IPlayerPosition playerPosition,ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol)
        {
            _protocol = protocol;
            _playerPosition = playerPosition;
            
            //_startPositionSendがないとプレイヤーの座標が0,0,0の時にプレイヤー座標が送信されるため、
            //不要なチャンクデータの受信など不都合が発生する可能性がある（チャンクのデータはプレイヤーの周りの情報が帰ってくる）
            //そのため、ハンドシェイクが終わってからプレイヤー座標の送信を始める
            receiveInitialHandshakeProtocol.OnFinishHandshake += _ => _startPositionSend = true;
        }

        private float _timer;
        
        /// <summary>
        /// Updateと同じタイミングで呼ばれる
        /// </summary>
        public void Tick()
        {
            if (!_startPositionSend)
            {
                return;
            }
            
            
            _timer += Time.deltaTime;
            if (_timer < NetworkConst.UpdateIntervalSeconds) return;
            _timer = 0;
            _protocol.Send(_playerPosition.GetPlayerPosition());
        }
    }
}