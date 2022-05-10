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
        
        private const float Interval = 0.2f;
        
        public PlayerPositionSender(SendPlayerPositionProtocolProtocol protocol, IPlayerPosition playerPosition)
        {
            _protocol = protocol;
            _playerPosition = playerPosition;
        }

        private float _timer;
        public void Tick()
        {
            _timer += Time.deltaTime;
            if (_timer < Interval) return;
            _timer = 0;
            _protocol.Send(_playerPosition.GetPlayerPosition());
        }
    }
}