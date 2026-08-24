using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Network.API;
using Client.Playtest;
using Client.Playtest.Operations;
using UnityEngine;
using VContainer;

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

    await p.Until(() => clientDatastore.IsNearFieldInstantiated.Value, 180f, "mapObject近傍生成が完了する");

    p.Note("サーバーのmapObject件数とクライアント生成数を突き合わせる");

    var handshake = ClientDIContext.DIContainer.DIContainerResolver.Resolve<InitialHandshakeResponse>();
    var nearFieldOrder = MapObjectLayoutDistanceOrder.SortNearFieldFirst(
        handshake.MapLayout.MapObjects, handshake.PlayerPos);
    var serverCount = nearFieldOrder.NearFieldCount;
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
    var sampleGuid = new System.Guid(nearFieldOrder.Entries[0].Layout.MapObjectGuid);
    var nearest = clientDatastore.SearchNearestMapObject(new HashSet<Guid> { sampleGuid }, p.PlayerPosition);
    p.Assert(nearest != null, "SearchNearestMapObjectが生成済みmapObjectを引ける");

    await p.WaitSeconds(1f);
    await p.Screenshot("02-mapobjects-around-player");
});
