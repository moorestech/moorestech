using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Entity.Object
{
    public class TrainCarEntityChildrenObject : MonoBehaviour, IDeleteTarget
    {
        public TrainCarEntityObject TrainCarEntityObject { get; private set; }
        
        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            TrainCarEntityObject = trainCarEntityObject;
        }
        public void SetRemovePreviewing()
        {
            TrainCarEntityObject.SetRemovePreviewing();
        }

        public void ResetMaterial()
        {
            TrainCarEntityObject.ResetMaterial();
        }
        
        public bool IsRemovable(out string reason)
        {
            reason = null;
            return true;
        }
    }
}