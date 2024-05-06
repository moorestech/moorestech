using System.Collections.Generic;

namespace Game.Gear
{
    public class GearNetworkDatastore
    {
        private Dictionary<int,GearNetwork> _blockEntityToGearNetwork;
        
        public void AddGear(IGearComponent gear)
        {
            var connectedNetworkIds = new HashSet<int>();
            foreach (var connectedGear in gear.ConnectingGears)
            {
                //新しく設置された歯車に接続している歯車は、すべて既存のNWに接続している前提
                var networkId = _blockEntityToGearNetwork[connectedGear.EntityId].NetworkId;
                connectedNetworkIds.Add(networkId);
            }
        }
    }
}