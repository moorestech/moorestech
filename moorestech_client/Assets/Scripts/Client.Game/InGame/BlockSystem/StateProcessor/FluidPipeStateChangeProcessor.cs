using Game.Fluid;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class FluidPipeStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private Transform waterLevelTransform;
        public FluidPipeStateDetail CurrentFluidPipeState { get; private set; }
        
        private void Update()
        {
            if (CurrentFluidPipeState == null) return;
            
            UpdateWaterLevel(CurrentFluidPipeState);
        }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            CurrentFluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.FluidPipeStateDetailKey);
        }
        
        public void UpdateWaterLevel(FluidPipeStateDetail detail)
        {
            var waterLevel = detail.Amount / detail.Capacity;
            waterLevelTransform.position = new Vector3(0.5f, (waterLevel + 0.5f) * 0.5f, 0.5f);
            waterLevelTransform.localScale = new Vector3(Mathf.Sqrt(1 - Mathf.Pow(waterLevel * 2 - 1, 2)) * 0.5f, 1, 1);
        }
    }
}