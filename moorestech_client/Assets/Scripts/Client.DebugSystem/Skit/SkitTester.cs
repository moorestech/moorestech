using Client.Game.InGame.Block;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.Skit;
using Client.Skit.Skit;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using Server.Boot;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.DebugSystem.Skit
{
    public class SkitTester : MonoBehaviour
    {
        [SerializeField] private SkitManager skitManager;
        [SerializeField] private BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] private EnvironmentRoot environmentRoot;
        [SerializeField] private SkitUI skitUI;
        
        private IObjectResolver _resolver;
        
        private void Awake()
        {
            // DIコンテナでSkitManagerの依存を解決
            var builder = new ContainerBuilder();
            
            // 必要な依存関係の登録
            builder.Register<SkitFireManager>(Lifetime.Singleton);

            // SkitManagerはISkitActionControllerを要求するため両インターフェースで公開する
            // SkitManager requires ISkitActionController, so expose both interfaces
            builder.Register<SkitActionContext>(Lifetime.Singleton).As<ISkitActionController>().As<ISkitActionContext>();

            // Hierarchy上のコンポーネントを登録
            builder.RegisterComponent(skitManager);
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(environmentRoot);
            builder.RegisterComponent(skitUI);
            builder.RegisterInstance<IMapObjectPin>(new MapObjectTest());
            builder.RegisterInstance<IVeinPin>(new MapObjectTest());

            // テストシーンにmapObject/エンティティは存在しないのでSetActive先の空オブジェクトだけ用意する
            // The test scene has no map objects or entities, so provide empty objects purely as SetActive targets
            var mapObjectDatastore = CreateChildComponent<MapObjectGameObjectDatastore>();
            var entityObjectDatastore = CreateChildComponent<EntityObjectDatastore>();

            // RegisterComponentはビルド時に強制Resolveしサーバ応答必須のConstructを走らせるためRegisterInstanceを使う
            // RegisterComponent force-resolves at build time and would run Construct, which needs a server response, so use RegisterInstance
            builder.RegisterInstance(mapObjectDatastore);
            builder.RegisterInstance(entityObjectDatastore);

            // 依存関係を解決
            _resolver = builder.Build();
            _resolver.Inject(skitManager);
            
            var options = new MoorestechServerDIContainerOptions(ServerDirectory.GetDirectory());
            
            new MoorestechServerDIContainerGenerator().Create(options);
            
            skitManager.StartSkit("Vanilla/Skit/skits/100_start_game").Forget();

            #region Internal

            T CreateChildComponent<T>() where T : Component
            {
                var child = new GameObject(typeof(T).Name);
                child.transform.SetParent(transform);
                return child.AddComponent<T>();
            }

            #endregion
        }

        private void OnDestroy()
        {
            _resolver?.Dispose();
        }
    }
}
