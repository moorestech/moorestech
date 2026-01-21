using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    public class DeleteTargetRail : MonoBehaviour, IDeleteTarget
    {       
        public BezierRailChain RailChain { get; private set; }
        
        public void SetParentBezierRailChain(BezierRailChain parent)
        {
            RailChain = parent;
        }

        public void SetRemovePreviewing()
        {
            RailChain.SetRemovePreviewing();
        }
        public void ResetMaterial()
        {
            RailChain.ResetMaterial();
        }
    }
}