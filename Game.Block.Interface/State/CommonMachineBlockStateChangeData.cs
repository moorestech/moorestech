using System;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     
    /// </summary>
    [Serializable]
    public class CommonMachineBlockStateChangeData
    {

        ///     
        ///     

        public float PowerRate;


        ///     

        public float ProcessingRate;

        public CommonMachineBlockStateChangeData(float currentPower, float requestPower, float processingRate)
        {
            PowerRate = requestPower == 0 ? 1.0f : currentPower / requestPower;
            ProcessingRate = processingRate;
        }
    }
}