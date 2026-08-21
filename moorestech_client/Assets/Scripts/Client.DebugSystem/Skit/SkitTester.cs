using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Skit;
using Client.Game.InGame.Tutorial;
using Client.Game.Skit;
using Client.Skit.Skit;
using Client.Skit.UI;
using CommandForgeGenerator.Command;
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
            // SkitManagerはIReadOnlyList<ITutorialWorldPin>でピンをまとめて抑止する
            // SkitManager suppresses pins as a whole through IReadOnlyList<ITutorialWorldPin>
            builder.RegisterInstance<IReadOnlyList<ITutorialWorldPin>>(new List<ITutorialWorldPin> { new MapObjectTest() });

            // テストシーンに実体が無いためSetActive先の空ダミーのみ用意
            // The test scene has no real objects, so provide empty dummies purely as SetActive targets
            var mapObjectDatastore = CreateChildComponent<MapObjectGameObjectDatastore>();
            var outcropDatastore = CreateChildComponent<OutcropGameObjectDatastore>();
            var entityObjectDatastore = CreateChildComponent<EntityObjectDatastore>();

            // RegisterComponentはビルド時に強制Resolveしサーバ応答必須のConstructを走らせるためRegisterInstanceを使う
            // RegisterComponent force-resolves at build time and would run Construct, which needs a server response, so use RegisterInstance
            builder.RegisterInstance(mapObjectDatastore).AsSelf().As<ISkitWorldObjectControl>();
            builder.RegisterInstance(outcropDatastore).AsSelf().As<ISkitWorldObjectControl>();
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
