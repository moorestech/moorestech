using Game.Fluid;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class FluidPipeStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        private static readonly int WaterLevelProperty = Shader.PropertyToID("_WaterLevel");
        [SerializeField] private Transform waterLevelTransform;
        [SerializeField] private MeshRenderer meshRenderer;
        
        public FluidPipeStateDetail CurrentFluidPipeState { get; private set; }
        
        private void Update()
        {
            if (CurrentFluidPipeState == null) return;
            
            UpdateWaterLevel(CurrentFluidPipeState.Amount / CurrentFluidPipeState.Capacity);
        }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            CurrentFluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.FluidPipeStateDetailKey);
        }
        
        public void UpdateWaterLevel(float waterLevel)
        {
            waterLevelTransform.position = new Vector3(0.5f, (waterLevel + 0.5f) * 0.5f, 0.5f);
            waterLevelTransform.localScale = new Vector3(Mathf.Sqrt(1 - Mathf.Pow(waterLevel * 2 - 1, 2)) * 0.5f, 1, 1);
            
            meshRenderer.sharedMaterial.SetFloat(WaterLevelProperty, waterLevel);
        }
        
        public void ResetMaterial()
        {
        }
    }
}