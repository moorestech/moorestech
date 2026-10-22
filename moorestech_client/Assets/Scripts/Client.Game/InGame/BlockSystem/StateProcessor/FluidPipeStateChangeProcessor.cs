using System;
using Client.Game.InGame.Block;
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
            
            // Start前に届いた状態があればそれを、無ければ空を表示する
            // Show a state that arrived before Start, otherwise show empty
            ApplyCurrentWaterLevel();
        }
        
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            using (BlockStateProfilerMarkers.FluidPipeGetStateDetail.Auto()) _currentFluidPipeState = blockState.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.BlockStateDetailKey);
            
            // 共有マテリアルを書き換えないよう複製前は反映をStartへ任せる
            // Leave the update to Start until the material is cloned so the shared material is never written
            // silent-ok: 複製前の状態はStartのApplyCurrentWaterLevelが必ず反映する / 録画テストの水位表示で気づく
            if (_waterLevelMaterial == null) return;
            using (BlockStateProfilerMarkers.FluidPipeApplyWaterLevel.Auto()) ApplyCurrentWaterLevel();
        }
        
        private void ApplyCurrentWaterLevel()
        {
            var waterLevel = _currentFluidPipeState == null ? 0f : _currentFluidPipeState.Amount / _currentFluidPipeState.Capacity;
            UpdateWaterLevel(waterLevel);
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