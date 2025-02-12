using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    public class RailSaverComponent : IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        public string SaveKey => "RailSaverComponent";

        public RailSaverComponent(BlockPositionInfo blockPositionInfo_)
        {
        }


        public string GetSaveState()
        {
            return "";
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

    }
}
