using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityChildrenObject : MonoBehaviour
    {
        public TrainCarEntityObject TrainCarEntityObject { get; private set; }
        
        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            TrainCarEntityObject = trainCarEntityObject;
        }
    }
}