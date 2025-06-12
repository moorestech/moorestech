using UnityEngine;
using VContainer;

namespace GameState
{
    public sealed class GameStateManager : MonoBehaviour
    {
        private static GameStateManager _instance;
        public static GameStateManager Instance => _instance;

        public IBlockRegistry Blocks { get; private set; }
        public IPlayerState Player { get; private set; }
        public IEntityRegistry Entities { get; private set; }
        public IGameProgressState GameProgress { get; private set; }
        public IMapObjectRegistry MapObjects { get; private set; }

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
            IMapObjectRegistry mapObjectRegistry)
        {
            Blocks = blockRegistry;
            Player = playerState;
            Entities = entityRegistry;
            GameProgress = gameProgressState;
            MapObjects = mapObjectRegistry;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}