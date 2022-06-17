using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using MainGame.UnityView.Util;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class ItemImages
    {
        private readonly List<ItemViewData> _itemImageList = new ();
        private readonly ItemViewData _emptyItemImage = new(null,"Empty");
        private readonly ItemViewData _nothingIndexItemImage;

        public ItemImages(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            _nothingIndexItemImage = new ItemViewData(null,"Item not found");
            _itemImageList.Add(_emptyItemImage);
            LoadTexture(modDirectory,singlePlayInterface,initialViewLoadingDetector).Forget();
        }

        private async UniTask LoadTexture(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface,IInitialViewLoadingDetector initialViewLoadingDetector)
        {
            
            var textures = await ItemTextureLoader.GetItemTexture(modDirectory.Directory,singlePlayInterface);
            foreach (var texture in textures)
            {
                _itemImageList.Add(new ItemViewData(texture.texture2D.ToSprite(),texture.name));
            }
            initialViewLoadingDetector.FinishItemTextureLoading();
            
        }


        public ItemViewData GetItemView(int index)
        {
            if (_itemImageList.Count <= index)
            {
                return _nothingIndexItemImage;
            }

            return _itemImageList[index];
        }

        public int GetItemNum() { return _itemImageList.Count; }
    }

    public class ItemViewData
    {
        public readonly Sprite itemImage;
        public readonly string itemName;

        public ItemViewData(Sprite itemImage, string itemName)
        {
            this.itemImage = itemImage;
            this.itemName = itemName;
        }
    }
}