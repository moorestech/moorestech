using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Map.Interface.MapObject;
using UnityEngine;

// P2: mapObjectがシーンベイクではなくLayout応答から実行時Instantiateされることを実v8ワールドで検証する
// P2: verifies map objects are instantiated at runtime from the layout response on the real v8 world
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("map-object-runtime-instantiate", options, async p =>
{
    // v8ワールドは地形がEnvironment.prefabに焼かれているため足場生成はせず、ワールド既定スポーンに立つ
    // The v8 world bakes terrain into Environment.prefab, so skip the platform and stand at the world spawn
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig
    {
        CreateFlatGround = false,
        FreeBlockPlacement = false,
        SpawnPosition = new Vector3(186f, 17f, -37.4f)
    });

    p.Note("mapObjectの実行時Instantiate完了を待つ");

    var clientDatastore = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    p.Assert(clientDatastore != null, "クライアントのMapObjectGameObjectDatastoreがシーンに存在する");

    // 生成ループ完走で完了する保持タスク。skip+continue化後は失敗個体があっても必ず完走する
    // The retained task completed on loop completion; after skip+continue it always completes even with failing items
    await p.Until(() => clientDatastore.WaitForInitialApplyAsync().Status.IsCompletedSuccessfully(), 180f, "mapObject生成ループが完走する");

    p.Note("サーバーのmapObject件数とクライアント生成数を突き合わせる");

    // ServerService(=main ServiceProvider)にはIMapObjectDatastore未登録のためServerContextの静的参照を使う
    // The main ServiceProvider doesn't register IMapObjectDatastore, so use the ServerContext static handle
    var serverDatastore = Game.Context.ServerContext.MapObjectDatastore;
    var serverCount = serverDatastore.MapObjects.Count;
    var clientCount = clientDatastore.transform.childCount;
    p.Note($"server mapObjects={serverCount} / client instantiated={clientCount}");

    p.Assert(serverCount > 0, $"サーバーがmapObjectを保持している (={serverCount})");
    p.Assert(clientCount == serverCount, $"生成数がサーバー件数と一致する (client={clientCount} server={serverCount})");

    // 生成物がすべて実体として成立していること（root MapObjectGameObject・identity注入済み）を確認する
    // Confirm every instance is well-formed (root MapObjectGameObject with runtime identity injected)
    var instances = clientDatastore.GetComponentsInChildren<MapObjectGameObject>(true);
    var withIdentity = instances.Count(m => m.MapObjectGuid != System.Guid.Empty);
    p.Note($"MapObjectGameObject={instances.Length} / identity注入済み={withIdentity}");
    p.Assert(instances.Length == clientCount, $"全生成物のrootにMapObjectGameObjectがある (={instances.Length})");
    p.Assert(withIdentity == instances.Length, "全生成物にmapObjectGuidが注入されている");

    await p.Screenshot("01-spawn-mapobjects");

    p.Note("最寄りのmapObjectを引き当てて実体の位置整合を確認する");

    // 検索APIが実体を引けること＝生成物がワールド座標に正しく配置されていることの確認
    // The search API returning a hit means instances are placed at correct world positions
    var sampleGuid = serverDatastore.MapObjects.First().MapObjectGuid;
    var nearest = clientDatastore.SearchNearestMapObject(new[] { sampleGuid }, p.PlayerPosition);
    p.Assert(nearest != null, "SearchNearestMapObjectが生成済みmapObjectを引ける");

    await p.WaitSeconds(1f);
    await p.Screenshot("02-mapobjects-around-player");
});
