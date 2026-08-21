using System;
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.MapVein;
using Core.Master;
using Game.Map.Interface.Json;
using Mod.Config;
using Mod.Loader;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json;
using Server.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// map.jsonをエディタモードのシーンへ展開する。mapObjectはAddressableプレハブをPrefabリンク付きで生成する
// Expands map.json into the edit-mode scene; map objects are instantiated with their prefab links intact
public static class MapAuthoringImporter
{
    public const string RootObjectName = "MapAuthoringRoot";
    private const int ProgressUpdateInterval = 50;

    public static void Import(string mapJsonPath)
    {
        // マスタをロードしてguid→Addressableパス解決を可能にする
        // Load masters so guid → addressable path resolution works in edit mode
        var serverDirectory = ServerDirectory.GetDirectory();
        var modResource = new ModsResource(ServerConst.CreateServerModsDirectory(serverDirectory));
        MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

        var mapInfoJson = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapJsonPath));

        // 既存のオーサリングルートは破棄して作り直す
        // Discard and rebuild any existing authoring root
        var existingRoot = GameObject.Find(RootObjectName);
        if (existingRoot != null) Object.DestroyImmediate(existingRoot);

        var root = new GameObject(RootObjectName);
        var mapObjectCount = ImportMapObjects(CreateChild("MapObjects"));
        var veinCount = ImportVeins(CreateChild("MapVeins"));
        ImportSpawnPoint();

        EditorUtility.ClearProgressBar();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[MapAuthoring] Import完了 mapObjects:{mapObjectCount}/{mapInfoJson.MapObjects.Count} mapVeins:{veinCount}/{mapInfoJson.MapVeins.Count} path:{mapJsonPath}");

        #region Internal

        Transform CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(root.transform);
            return child.transform;
        }

        int ImportMapObjects(Transform parent)
        {
            // 失敗guidもキャッシュし、同一guid千個規模でのロードとLogErrorを1回に抑える
            // Cache failures too, so load and LogError fire once per guid even at thousand-object scale
            var prefabCacheByGuid = new Dictionary<Guid, GameObject>();
            var importedCount = 0;

            for (var i = 0; i < mapInfoJson.MapObjects.Count; i++)
            {
                var info = mapInfoJson.MapObjects[i];
                if (i % ProgressUpdateInterval == 0)
                    EditorUtility.DisplayProgressBar("MapAuthoring", $"mapObjects {i}/{mapInfoJson.MapObjects.Count}", i / (float)mapInfoJson.MapObjects.Count);

                var prefab = ResolvePrefabOrNull(info.MapObjectGuid);
                if (prefab == null) continue;

                // Prefabリンクを保ったままシーンへ生成し、ID/GUIDをシリアライズフィールドへ注入する
                // Instantiate with the prefab link intact, then inject id/guid into the serialized fields
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.position = info.Position;

                // Exportが姿勢とスケールを書き出すため、戻さないとImport→Exportの往復でprefab既定値へ痩せる
                // Export writes the rotation and scale out, so skipping them here would shrink an import-export round trip back to the prefab defaults
                instance.transform.rotation = info.Rotation;
                instance.transform.localScale = info.Scale;

                var mapObject = instance.GetComponent<MapObjectGameObject>();
                if (mapObject == null)
                {
                    Debug.LogError($"[MapAuthoring] MapObjectGameObjectがrootに無いprefabです MapObjectGuid:{info.MapObjectGuidStr}");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                mapObject.SetRuntimeIdentity(info.InstanceId, info.MapObjectGuidStr);
                importedCount++;
            }

            return importedCount;

            GameObject ResolvePrefabOrNull(Guid mapObjectGuid)
            {
                if (prefabCacheByGuid.TryGetValue(mapObjectGuid, out var cached)) return cached;

                // マスタ欠落・ロード失敗はLogErrorして当該guidをskipする
                // Master-missing or load failure logs an error and skips that guid
                var element = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(mapObjectGuid);
                if (element == null)
                {
                    Debug.LogError($"[MapAuthoring] mapObjectマスタがありません MapObjectGuid:{mapObjectGuid}");
                    prefabCacheByGuid[mapObjectGuid] = null;
                    return null;
                }

                var prefab = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
                if (prefab == null)
                    Debug.LogError($"[MapAuthoring] prefabロード失敗 MapObjectGuid:{mapObjectGuid} AddressablePath:{element.AddressablePath}");

                prefabCacheByGuid[mapObjectGuid] = prefab;
                return prefab;
            }
        }

        int ImportVeins(Transform parent)
        {
            var importedCount = 0;
            foreach (var veinJson in mapInfoJson.MapVeins)
            {
                // item/fluidの区別はマスタのveinParam型から導出する
                // Item/fluid distinction is derived from the master's veinParam type
                var element = MasterHolder.MapVeinMaster.GetElementOrNull(veinJson.VeinGuid);
                if (element == null)
                {
                    Debug.LogError($"[MapAuthoring] mapVeinマスタがありません VeinGuid:{veinJson.VeinGuidStr}");
                    continue;
                }

                var veinObject = new GameObject($"Vein_{element.VeinName}");
                veinObject.transform.SetParent(parent);

                var component = element.VeinParam is FluidVeinParam
                    ? (MonoBehaviour)veinObject.AddComponent<FluidMapVeinGameObject>()
                    : veinObject.AddComponent<ItemMapVeinGameObject>();
                ApplyVeinPlacement(component, veinJson);
                importedCount++;
            }

            return importedCount;
        }

        void ApplyVeinPlacement(MonoBehaviour veinComponent, MapVeinInfoJson veinJson)
        {
            // NormalizeBoundsと同一規則(奇数サイズ軸はcenter0.5)でtransform位置を逆算する
            // Recompute the transform position using the same rule as NormalizeBounds (odd axes center at 0.5)
            var size = new Vector3Int(veinJson.MaxX - veinJson.MinX, veinJson.MaxY - veinJson.MinY, veinJson.MaxZ - veinJson.MinZ);
            var centerOffset = new Vector3(size.x % 2 == 0 ? 0f : 0.5f, size.y % 2 == 0 ? 0f : 0.5f, size.z % 2 == 0 ? 0f : 0.5f);
            veinComponent.transform.position = new Vector3(
                (veinJson.MinX + veinJson.MaxX) / 2f - centerOffset.x,
                (veinJson.MinY + veinJson.MaxY) / 2f - centerOffset.y,
                (veinJson.MinZ + veinJson.MaxZ) / 2f - centerOffset.z);

            // Item/Fluidで共通interfaceが無いため、シリアライズフィールドへ直接書き込む
            // No shared interface between item/fluid, so write the serialized fields directly
            var serializedVein = new SerializedObject(veinComponent);
            serializedVein.FindProperty("veinGuid").stringValue = veinJson.VeinGuidStr;
            serializedVein.FindProperty("bounds").boundsValue = new Bounds(centerOffset, size);
            serializedVein.ApplyModifiedPropertiesWithoutUndo();
        }

        void ImportSpawnPoint()
        {
            var spawnObject = new GameObject("SpawnPoint");
            spawnObject.transform.SetParent(root.transform);
            spawnObject.transform.position = mapInfoJson.DefaultSpawnPointJson.Position;
            spawnObject.AddComponent<SpawnPointObject>();
        }

        #endregion
    }
}
