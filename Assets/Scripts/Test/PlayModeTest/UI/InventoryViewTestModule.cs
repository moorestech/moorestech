using System.Collections.Generic;
using System.Linq;
using MainGame.GameLogic.Event;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace Test.PlayModeTest.UI
{
    public class InventoryViewTestModule : MonoBehaviour
    {
        [SerializeField] private MainInventoryItemView mainInventoryItemView;
        [SerializeField] private ItemImages itemImages;

        private void Start()
        {
            var update = new InventoryUpdateEvent();
            mainInventoryItemView.Construct(update,itemImages);
            
            //slot id count
            List<(int,int,int)> items = new List<(int,int,int)>();

            //アイテムの設定
            items.Add((0,1,5));
            items.Add((5,2,10));
            items.Add((10,2,1));
            items.Add((40,2,1));
            items.Add((44,2,1));

            //イベントを発火
            foreach (var item in items)
            {
                update.OnOnInventoryUpdate(item.Item1,item.Item2,item.Item3);
            }
            
            //アイテムのUIの取得
            var slots = mainInventoryItemView.GetInventoryItemSlots();

            
            //チェック
            foreach (var item in items)
            {
                var slot = item.Item1;
                var id = item.Item2;
                var count = item.Item3;

                var expectedCount = count.ToString();
                var actualCount = slots[slot].gameObject.GetComponentInChildren<TextMeshProUGUI>().text;
                Assert.AreEqual(expectedCount,actualCount);

                //同じ画像かチェック
                var expectedImage = itemImages.GetItemImage(id).GetHashCode();
                //ButtonにもImageがついてあるため、Last()を取得する
                var actualImage = slots[slot].GetComponentsInChildren<Image>().Last().sprite.GetHashCode();
                Assert.AreEqual(expectedImage,actualImage);
            }
            
            
        }
    }
}