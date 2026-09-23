using System.Threading;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.BeltSegment;
namespace Client.Game.InGame.BeltSegment.Network
{
    internal interface IBeltSnapshotRequester
    {
        UniTask<BeltWorldSnapshot> Request(CancellationToken cancellationToken);
    }
    internal sealed class BeltSnapshotRequester : IBeltSnapshotRequester
    {
        private readonly VanillaApiWithResponse api;
        internal BeltSnapshotRequester(VanillaApiWithResponse api) => this.api = api;
        public UniTask<BeltWorldSnapshot> Request(CancellationToken cancellationToken) => api.GetBeltWorld(cancellationToken);
    }
}
