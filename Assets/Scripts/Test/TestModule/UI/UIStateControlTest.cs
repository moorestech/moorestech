using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.Control.UI.UIState;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.Network.Settings;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class UIStateControlTest : MonoBehaviour
    {
        [SerializeField]private UIStateControl uiStateControl;
        [SerializeField] private BlockClickDetectTest blockClickDetectTest;

        private void Start()
        {
            var inventory = new RequestPlayerInventoryProtocol(new TestSocketModule(), new PlayerConnectionSetting(0));
            
            //Constructが依存関係の増加によって構築するのが面倒になったのでとりあえずコメントアウトして放置する
            //uiStateControl.Construct(blockClickDetectTest,inventory,new RequestBlockInventoryProtocol(new TestSocketModule()));
        }
    }
}