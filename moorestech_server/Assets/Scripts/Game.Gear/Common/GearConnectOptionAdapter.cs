using Mooresmaster.Model.GearModule;

namespace Game.Gear.Common
{
    /// <summary>
    /// GearConnectsItemOptionをIGearConnectOptionに変換するアダプタ
    /// Adapter to convert GearConnectsItemOption to IGearConnectOption
    /// </summary>
    public class GearConnectOptionAdapter : IGearConnectOption
    {
        public bool IsReverse { get; }

        public GearConnectOptionAdapter(GearConnectsItemOption option)
        {
            IsReverse = option.IsReverse;
        }

        public GearConnectOptionAdapter(bool isReverse)
        {
            IsReverse = isReverse;
        }
    }
}
