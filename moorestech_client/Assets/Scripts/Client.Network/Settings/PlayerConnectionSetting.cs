using System;

namespace MainGame.Network.Settings
{
    public class PlayerConnectionSetting
    {
        public readonly int PlayerId;

        public PlayerConnectionSetting(int playerId)
        {
            PlayerId = playerId;
        }
    }
}