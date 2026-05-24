using System.Collections.Generic;
using Client.Game.InGame.Player.StateController.State;

namespace Client.Game.InGame.Player.StateController
{
    public class PlayerStateDictionary
    {
        private readonly Dictionary<PlayerStateEnum, IPlayerState> _stateDictionary = new();

        public PlayerStateDictionary(
            NormalPlayerState normalPlayerState,
            RidingPlayerState ridingPlayerState)
        {
            _stateDictionary.Add(PlayerStateEnum.Normal, normalPlayerState);
            _stateDictionary.Add(PlayerStateEnum.Riding, ridingPlayerState);
        }

        public IPlayerState GetState(PlayerStateEnum state)
        {
            return _stateDictionary[state];
        }
    }
}
