using Constant.Server;
using MainGame.Network.Receive;
using MainGame.Network.Send;
using MainGame.UnityView.Player;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.Player
{
    public class PlayerPositionSender : ITickable
    {
        private readonly IPlayerObjectController _playerObjectController;
        private readonly SendPlayerPositionProtocolProtocol _protocol;


        private bool _startPositionSend;

        private float _timer;

        public PlayerPositionSender(SendPlayerPositionProtocolProtocol protocol, IPlayerObjectController playerObjectController)
        {
            _protocol = protocol;
            _playerObjectController = playerObjectController;


        }

        /// <summary>
        ///     Updateと同じタイミングで呼ばれる
        /// </summary>
        public void Tick()
        {
            if (!_startPositionSend) return;


            _timer += Time.deltaTime;
            if (_timer < NetworkConst.UpdateIntervalSeconds) return;
            _timer = 0;
            _protocol.Send(_playerObjectController.Position2d);
        }
    }
}