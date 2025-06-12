using Client.Network.API;

namespace GameState
{
    /// <summary>
    /// Interface for GameState implementations that need to connect to VanillaApi for network updates
    /// </summary>
    internal interface IVanillaApiConnectable
    {
        /// <summary>
        /// Connects the implementation to VanillaApi for receiving server events
        /// </summary>
        /// <param name="initialHandshakeResponse">Initial game state from server handshake</param>
        void ConnectToVanillaApi(InitialHandshakeResponse initialHandshakeResponse);
    }
}