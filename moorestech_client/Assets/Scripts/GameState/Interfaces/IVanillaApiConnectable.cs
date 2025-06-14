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
        /// <param name="vanillaApi">The VanillaApi instance for network communication</param>
        /// <param name="initialHandshakeResponse">Initial game state from server handshake</param>
        void ConnectToVanillaApi(VanillaApi vanillaApi, InitialHandshakeResponse initialHandshakeResponse);
    }
}