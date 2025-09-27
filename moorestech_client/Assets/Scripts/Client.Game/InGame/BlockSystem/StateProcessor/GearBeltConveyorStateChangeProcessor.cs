using System;
using Client.Game.InGame.Block;
using Game.Gear.Common;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    public class GearBeltConveyorStateChangeProcessor : MonoBehaviour, IBlockStateChangeProcessor
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float speed = 1;
        
        private GearStateDetail _currentGearState;
        
        private Vector2 _offset;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        
        public void Initialize(BlockGameObject blockGameObject) { }
        
        public void OnChangeState(BlockStateMessagePack blockState)
        {
            _currentGearState = blockState.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
        }
    }
}