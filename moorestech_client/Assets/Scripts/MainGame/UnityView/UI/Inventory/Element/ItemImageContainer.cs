using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using SinglePlay;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.Element
{
    /// <summary>
    /// TODO ここもItemConfigと統合して、クライアント向けのリソースも含めたアイテムリストを作りたい
    /// </summary>
    public class ItemImageContainer
    {
        private readonly List<ItemViewData> _itemImageList = new();
        private readonly ItemViewData _nothingIndexItemImage;

        public ItemImageContainer(ModDirectory modDirectory, SinglePlayInterface singlePlayInterface)
        {
            _nothingIndexItemImage = new ItemViewData(null, null, new ItemConfigData("Not item", 100, "Not mod", 0));

            LoadTexture(modDirectory, singlePlayInterface).Forget();
        }

        public event Action OnLoadFinished;

        /// <summary>
        ///  テクスチャのロードは別スレッドで非同期で行いたいのでUniTaskをつける
        /// </summary>
        private async UniTask LoadTexture(ModDirectory modDirectory, SinglePlayInterface singlePlayInterface)
        {
            //await BlockGlbLoader.GetBlockLoaderは同期処理になっているため、ここで1フレーム待って他のイベントが追加されるのを待つ
            await UniTask.WaitForFixedUpdate();

            _itemImageList.Add(null); //id 0番は何もないことを表すのでnullを入れる
            
            var textures = ItemTextureLoader.GetItemTexture(modDirectory.Directory, singlePlayInterface);
            _itemImageList.AddRange(textures);

            OnLoadFinished?.Invoke();
        }


        public ItemViewData GetItemView(int index)
        {
            if (_itemImageList.Count <= index)
            {
                Debug.Log("存在しないアイテムIDです。");
                return _nothingIndexItemImage;
            }
            

            return _itemImageList[index];
        }

    }
}