using System;
using System.Collections.Generic;
using System.IO;
using Client.Ui.Content;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// mock-host の DEMO の絵（種番号の PNG）を返すテクスチャ源。並べ比べの撮影だけが使う
    /// Texture source returning the mock-host DEMO pictures (PNG per seed); used only by the comparison capture
    /// </summary>
    public sealed class BuildMenuReferenceDemoTextures : IGameContentTextureSource
    {
        // fixture の貨物車両は blockIconUrl(5)、電線接続ツールは blockIconUrl(6)
        // The fixture cargo car uses blockIconUrl(5) and the wire connect tool blockIconUrl(6)
        private const int TrainCarSeed = 5;
        private const int ConnectToolSeed = 6;

        private readonly Dictionary<int, Texture2D> _bySeed = new();

        public BuildMenuReferenceDemoTextures(string directory, int maxSeed)
        {
            // 撮影の道具の出力が欠けていたら撮らずに止める（白や名前で代えると参照と違う絵で比べてしまう）
            // Stop before capturing if the tool output is missing (substituting white or names would compare against different pictures)
            for (var seed = 1; seed <= maxSeed; seed++)
            {
                var path = Path.Combine(directory, $"block-{seed}.png");
                if (!File.Exists(path)) throw new FileNotFoundException("Run tools/uitk/render-mock-placeholder-icons.mjs first (Task 10 Step 2b)", path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(path));
                _bySeed[seed] = texture;
            }
        }

        public Texture2D BlockTexture(BlockId id) => _bySeed[id.AsPrimitive()];
        public Texture2D TrainCarTexture(Guid trainCarGuid) => _bySeed[TrainCarSeed];
        public Texture2D ConnectToolTexture(Guid connectToolGuid) => _bySeed[ConnectToolSeed];

        // ビルドメニューの面はアイテム・液体の絵を描かない。呼ばれたら撮影の前提が崩れている
        // The build menu face draws no item or fluid picture; a call means the capture premise is broken
        public Texture2D ItemTexture(ItemId id) => throw new NotSupportedException("The build menu comparison has no item pictures");
        public Texture2D FluidTexture(FluidId id) => throw new NotSupportedException("The build menu comparison has no fluid pictures");
    }
}
