using System;
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
        
        private FluidPipeStateDetail _currentFluidPipeState;
        private Material _waterLevelMaterial;
        
        private void Start()
        {
            if (meshRenderer == null) return;
            
            _waterLevelMaterial = Instantiate(meshRenderer.sharedMaterial);
            meshRenderer.sharedMaterial = _waterLevelMaterial;
            UpdateWaterLevel(0);
        }
        
        private void Update()
        {
            if (_currentFluidPipeState == null) return;
            
            UpdateWaterLevel(_currentFluidPipeState.Amount / _currentFluidPipeState.Capacity);
        }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentFluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.BlockStateDetailKey);
        }
        
        public void UpdateWaterLevel(float waterLevel)
        {
            if (waterLevelTransform == null) return;
            
            waterLevelTransform.localPosition = new Vector3(0.5f, (waterLevel + 0.5f) * 0.5f, 0.5f);
            waterLevelTransform.localScale = new Vector3(Mathf.Sqrt(1 - Mathf.Pow(waterLevel * 2 - 1, 2)) * 0.5f, 1, 1);
            
            meshRenderer.sharedMaterial.SetFloat(WaterLevelProperty, waterLevel);
        }
        
        public void ResetMaterial()
        {
        }
        
        #if UNITY_EDITOR
        public FluidPipeStateDetail DebugCurrentFluidPipeState => _currentFluidPipeState;
        #endif
    }
}