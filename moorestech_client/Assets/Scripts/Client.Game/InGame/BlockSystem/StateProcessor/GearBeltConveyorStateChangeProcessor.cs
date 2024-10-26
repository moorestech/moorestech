using System;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearBeltConveyorStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float speed = 1;
        
        public GearStateDetail CurrentGearState { get; private set; }
        
        private Vector2 _offset;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        
        public void OnChangeState(ChangeBlockStateMessagePack blockState)
        {
            CurrentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }
        
        private void Update()
        {
            _offset.x += CurrentGearState.CurrentRpm / 60 * Time.time * speed;
            meshRenderer.sharedMaterial.SetTextureOffset(BaseMap, _offset);
        }
    }
}