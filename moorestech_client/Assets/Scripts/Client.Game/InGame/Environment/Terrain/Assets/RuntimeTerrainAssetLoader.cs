using System.Threading;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.Environment.Terrain.Assets
{
    public sealed class RuntimeTerrainAssetLoader : ITerrainAssetLoader
    {
        public UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            return AddressableLoader.LoadAsyncDefault<T>(address, cancellationToken);
        }
    }
}
