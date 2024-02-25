using System;

namespace MainGame.Network.Settings
{
    public class PlayerConnectionSetting
    {
        [Obsolete("これ以外にも、ゲーム全般に関する各種データをstatic経由で簡単に取得できるようにする")]
        public static PlayerConnectionSetting Instance { get; private set; }
        
        
        public readonly int PlayerId;

        public PlayerConnectionSetting(int playerId)
        {
            Instance = this;
            PlayerId = playerId;
        }
    }
}