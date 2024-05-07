using System;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class PlaceBlockState : IUIState
    {
        public PlaceBlockState()
        {
            Debug.Log("Create PlaceBlockState");
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            throw new NotImplementedException();
        }
        public UIStateEnum GetNext()
        {
            throw new NotImplementedException();
        }
        public void OnExit()
        {
            throw new NotImplementedException();
        }
    }
}