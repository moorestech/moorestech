using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.Environment.Terrain.Assets
{
    public interface ITerrainAssetLoader
    {
        UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : UnityEngine.Object;
    }
}
