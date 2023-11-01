using System;
using System.Collections.Generic;
using Core.Const;
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
        private readonly ItemViewData _nothingIndexItemImage;

        public ItemImages(ModDirectory modDirectory,SinglePlayInterface singlePlayInterface)
        {
            _nothingIndexItemImage = new ItemViewData(null,null,"Item not found",ItemConst.EmptyItemId);
            
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
            for (var i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                //idは1から始まるので+1する
                var itemId = i + 1;
                _itemImageList.Add(new ItemViewData(texture.texture2D.ToSprite(),texture.texture2D, texture.name,itemId));
            }
            OnLoadFinished?.Invoke();
        }


        public ItemViewData GetItemView(int index)
        {
            //item idは1から始まるのでマイナス１する
            index--;
            
            if (index < 0 || _itemImageList.Count <= index)
            {
                return _nothingIndexItemImage;
            }

            return _itemImageList[index];
        }

        public int GetItemNum() { return _itemImageList.Count; }
    }

    public class ItemViewData
    {
        public readonly Sprite ItemImage;
        public readonly Texture ItemTexture;
        public readonly string ItemName;
        public readonly int ItemId;

        public ItemViewData(Sprite itemImage, Texture itemTexture, string itemName, int itemId)
        {
            this.ItemImage = itemImage;
            this.ItemName = itemName;
            this.ItemId = itemId;
            ItemTexture = itemTexture;
        }
    }
}