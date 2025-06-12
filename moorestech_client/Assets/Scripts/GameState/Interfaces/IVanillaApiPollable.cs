using Client.Network.API;
using Cysharp.Threading.Tasks;

namespace GameState
{
    /// <summary>
    /// Interface for GameState implementations that need to receive periodic world data updates
    /// </summary>
    internal interface IVanillaApiPollable
    {
        /// <summary>
        /// Updates the implementation with polled world data
        /// </summary>
        /// <param name="worldData">The latest world data from server</param>
        UniTask UpdateWithWorldData(WorldDataResponse worldData);
    }
}