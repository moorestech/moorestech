using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainRailObject : MonoBehaviour
    {
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
    }
}