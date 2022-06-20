using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class ItemImages
    {
        public event Action OnLoadFinished;
        
        private readonly List<ItemViewData> _itemImageList = new ();
        private readonly ItemViewData _emptyItemImage = new(null,"Empty");
        private readonly ItemViewData _nothingIndexItemImage;

        public ItemImages(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface)
        {
            _nothingIndexItemImage = new ItemViewData(null,"Item not found");
            _itemImageList.Add(_emptyItemImage);
            LoadTexture(modDirectory,singlePlayInterface).Forget();
        }
        /// <summary>
        /// テクスチャのロードは非同期で行いたいのでUniTaskをつける
        /// </summary>
        private async UniTask LoadTexture(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();
            
            var textures = ItemTextureLoader.GetItemTexture(modDirectory.Directory,singlePlayInterface);
            foreach (var texture in textures)
            {
                _itemImageList.Add(new ItemViewData(texture.texture2D.ToSprite(),texture.name));
            }
            OnLoadFinished?.Invoke();
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