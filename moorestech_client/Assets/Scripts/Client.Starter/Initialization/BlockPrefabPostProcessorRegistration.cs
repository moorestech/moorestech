using System;
using Client.Common.Asset;
using Client.Common.PrefabPostProcess;
using Client.Game.InGame.Block.GearRotation.PrefabPostProcess;
using Cysharp.Threading.Tasks;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// ブロックのprefabにロード時にかける後処理の並びを決める。並びが実行順
    /// ブロックの読み込みはメインゲームのDIより前に走るので、DIではなくここで明示的に組み立てる
    /// Decides the post-process steps applied to block prefabs at load; the order is the execution order
    /// Block loading runs before the main game DI exists, so the steps are assembled explicitly here instead of through DI
    /// </summary>
    internal static class BlockPrefabPostProcessorRegistration
    {
        public static async UniTask<PrefabPostProcessPipeline> CreateAsync()
        {
            var gearSettings = await AddressableLoader.LoadAsyncDefault<GearRotationRenderSettings>(GearRotationRenderSettings.AddressablePath);
            if (gearSettings == null) throw new InvalidOperationException($"歯車の回転設定が読めません。Address:{GearRotationRenderSettings.AddressablePath}");

            // TODO: メッシュをまとめたい後処理が増えたらここへ足す(例: ブロックの動かない部分をまとめる)。動く部品を先に使用済みにし、後の後処理が避けられるようにする
            // TODO: Add more steps here as other mesh merging is needed (e.g. merging static block parts); moving parts are claimed first so later steps can skip them
            return new PrefabPostProcessPipeline("BlockPrefabTemplates", new IPrefabPostProcessor[]
            {
                new GearRotationPrefabPostProcessor(gearSettings),
            });
        }
    }
}
