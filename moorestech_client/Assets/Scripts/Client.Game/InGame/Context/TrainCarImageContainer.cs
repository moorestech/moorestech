using System;
using System.Collections.Generic;
using Client.Mod.Texture;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     車両のアイコン画像を車両Guidキーで管理するクラス
    ///     Holds train car icon images keyed by train car Guid
    /// </summary>
    public class TrainCarImageContainer
    {
        private readonly Dictionary<Guid, ItemViewData> _trainCarImageList = new();

        public ItemViewData GetTrainCarView(Guid trainCarGuid)
        {
            if (_trainCarImageList.TryGetValue(trainCarGuid, out var view)) return view;

            Debug.LogError($"TrainCarViewData not found. trainCarGuid:{trainCarGuid}");
            return null;
        }

        public void AddTrainCarView(Guid trainCarGuid, ItemViewData itemViewData)
        {
            _trainCarImageList[trainCarGuid] = itemViewData;
        }
    }
}
