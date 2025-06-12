using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GameState
{
    public sealed class GameStateManager : MonoBehaviour, IInitializable
    {
        private static GameStateManager _instance;
        public static GameStateManager Instance => _instance;

        public IBlockRegistry Blocks { get; private set; }
        public IPlayerState Player { get; private set; }
        public IEntityRegistry Entities { get; private set; }
        public IGameProgressState GameProgress { get; private set; }
        public IMapObjectRegistry MapObjects { get; private set; }
        
        private InitialHandshakeResponse _initialHandshakeResponse;
        private CancellationTokenSource _pollingCancellation;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [Inject]
        public void Construct(
            IBlockRegistry blockRegistry,
            IPlayerState playerState,
            IEntityRegistry entityRegistry,
            IGameProgressState gameProgressState,
            IMapObjectRegistry mapObjectRegistry,
            InitialHandshakeResponse initialHandshakeResponse)
        {
            Blocks = blockRegistry;
            Player = playerState;
            Entities = entityRegistry;
            GameProgress = gameProgressState;
            MapObjects = mapObjectRegistry;
            _initialHandshakeResponse = initialHandshakeResponse;
        }
        
        public void Initialize()
        {
            // Initialize registry implementations with VanillaApi access
            if (Blocks is IVanillaApiConnectable blockConnectable)
                blockConnectable.ConnectToVanillaApi(_initialHandshakeResponse);
                
            if (Player is IVanillaApiConnectable playerConnectable)
                playerConnectable.ConnectToVanillaApi(_initialHandshakeResponse);
                
            if (Entities is IVanillaApiConnectable entityConnectable)
                entityConnectable.ConnectToVanillaApi(_initialHandshakeResponse);
                
            if (GameProgress is IVanillaApiConnectable progressConnectable)
                progressConnectable.ConnectToVanillaApi(_initialHandshakeResponse);
                
            if (MapObjects is IVanillaApiConnectable mapObjectConnectable)
                mapObjectConnectable.ConnectToVanillaApi(_initialHandshakeResponse);
            
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
                    var worldData = await ClientContext.VanillaApi.Response.GetWorldData(ct);
                    
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
            
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}