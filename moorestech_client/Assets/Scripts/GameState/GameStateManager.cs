using System.Threading;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using GameState.Implementation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GameState
{
    public sealed class GameStateManager : IInitializable
    {
        public static GameStateManager Instance { get; private set; }
        
        public IBlockRegistry Blocks { get; private set; }
        public IPlayerState Player { get; private set; }
        public IEntityRegistry Entities { get; private set; }
        public IGameProgressState GameProgress { get; private set; }
        public IMapObjectRegistry MapObjects { get; private set; }
        
        private InitialHandshakeResponse _initialHandshakeResponse;
        private VanillaApi _vanillaApi;
        private CancellationTokenSource _pollingCancellation;

        public GameStateManager(
            InitialHandshakeResponse initialHandshakeResponse,
            VanillaApi vanillaApi)
        {
            Instance = this;
            
            Blocks = new BlockRegistry();
            Player = new PlayerState();
            Entities = new EntityRegistry();
            GameProgress = new GameProgressState();
            MapObjects = new MapObjectRegistry();
            _initialHandshakeResponse = initialHandshakeResponse;
            _vanillaApi = vanillaApi;
        }
        
        public void Initialize()
        {
            // Initialize registry implementations with VanillaApi access
            if (Blocks is IVanillaApiConnectable blockConnectable)
                blockConnectable.ConnectToVanillaApi(_vanillaApi, _initialHandshakeResponse);
                
            if (Player is IVanillaApiConnectable playerConnectable)
                playerConnectable.ConnectToVanillaApi(_vanillaApi, _initialHandshakeResponse);
                
            if (Entities is IVanillaApiConnectable entityConnectable)
                entityConnectable.ConnectToVanillaApi(_vanillaApi, _initialHandshakeResponse);
                
            if (GameProgress is IVanillaApiConnectable progressConnectable)
                progressConnectable.ConnectToVanillaApi(_vanillaApi, _initialHandshakeResponse);
                
            if (MapObjects is IVanillaApiConnectable mapObjectConnectable)
                mapObjectConnectable.ConnectToVanillaApi(_vanillaApi, _initialHandshakeResponse);
            
            // Start polling for world data updates
            StartWorldDataPolling();
        }
        
        private void StartWorldDataPolling()
        {
            _pollingCancellation = new CancellationTokenSource();
            PollWorldData(_pollingCancellation.Token).Forget();
        }
        
        private async UniTask PollWorldData(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Poll for world data updates
                    var worldData = await _vanillaApi.Response.GetWorldData(ct);
                    
                    // Update pollable registries
                    if (Blocks is IVanillaApiPollable blockPollable)
                        await blockPollable.UpdateWithWorldData(worldData);
                        
                    if (Entities is IVanillaApiPollable entityPollable)
                        await entityPollable.UpdateWithWorldData(worldData);
                    
                    // Add other pollable registries here as needed
                }
                catch (System.Exception e)
                {
                    // Log error but continue polling
                    Debug.LogError($"World data polling error: {e.Message}");
                }
                
                // Poll every second (matching EntityObjectDatastore behavior)
                await UniTask.Delay(1000, cancellationToken: ct);
            }
        }

        private void OnDestroy()
        {
            _pollingCancellation?.Cancel();
            _pollingCancellation?.Dispose();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}