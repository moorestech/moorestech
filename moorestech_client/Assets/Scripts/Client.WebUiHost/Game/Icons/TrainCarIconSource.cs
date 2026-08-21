using System;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/train-car-icons/{guid}.png で車両アイコンを解決する
    /// Resolves train-car icons served at GET /api/train-car-icons/{guid}.png
    /// </summary>
    public class TrainCarIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/train-car-icons/";

        public string PathPrefix => PathPrefixConst;

        public bool IsReady => ClientContext.TrainCarImageContainer != null;

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!Guid.TryParse(keyText, out var trainCarGuid)) return null;

            var view = ClientContext.TrainCarImageContainer.GetTrainCarView(trainCarGuid);
            return view?.ItemTexture as Texture2D;
        }
    }
}
