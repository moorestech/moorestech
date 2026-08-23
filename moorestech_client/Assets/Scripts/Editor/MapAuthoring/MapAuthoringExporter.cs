using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.MapVein;
using Game.Map.Interface.Json;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// シーン上のmapObject・mapVein・SpawnPointを走査してmap.jsonへ書き出す
// Scans map objects, veins, and the spawn point in the scene and writes them out as map.json
public static class MapAuthoringExporter
{
    public static void Export(string savePath)
    {
        // シーン残置物(旧テスト用Vein等)を拾わないよう、走査はオーサリングルート配下に限定する
        // Scan only under the authoring root so scene leftovers (old test veins etc.) are never picked up
        var root = GameObject.Find(MapAuthoringImporter.RootObjectName);
        if (root == null)
        {
            Debug.LogError($"[MapAuthoring] {MapAuthoringImporter.RootObjectName}がシーンにありません。先にImportを実行してください");
            return;
        }

        // 不正データはLogErrorで列挙し、1件でもあれば書き出さずに中断する
        // Invalid data is enumerated via LogError; abort without writing if any is found
        var hasError = false;

        var mapInfoJson = new MapInfoJson
        {
            DefaultSpawnPointJson = BuildSpawnPoint(),
            MapObjects = BuildMapObjects(),
            MapVeins = BuildMapVeins(),
        };

        if (hasError)
        {
            Debug.LogError("[MapAuthoring] 不正データがあるためExportを中断しました。上のエラーログを確認してください。");
            return;
        }

        File.WriteAllText(savePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[MapAuthoring] Export完了 mapObjects:{mapInfoJson.MapObjects.Count} mapVeins:{mapInfoJson.MapVeins.Count} path:{savePath}");

        #region Internal

        SpawnPointJson BuildSpawnPoint()
        {
            var spawnPoints = root.GetComponentsInChildren<SpawnPointObject>(true);
            if (spawnPoints.Length == 0)
            {
                Debug.LogError("[MapAuthoring] SpawnPointObjectがシーンにありません");
                hasError = true;
                return new SpawnPointJson();
            }

            if (spawnPoints.Length > 1)
                Debug.LogWarning($"[MapAuthoring] SpawnPointObjectが{spawnPoints.Length}個あります。最初の1個を使用します");

            var position = spawnPoints[0].transform.position;
            return new SpawnPointJson { X = position.x, Y = position.y, Z = position.z };
        }

        List<MapObjectInfoJson> BuildMapObjects()
        {
            // instanceIdは座標順の決定論的順序で全振り直しする(旧MapExportAndSettingと同方式)
            // Renumber every instanceId in a deterministic position order (same policy as the old exporter)
            var sceneMapObjects = root.GetComponentsInChildren<MapObjectGameObject>(true)
                .OrderBy(mapObject => mapObject.transform.position.x)
                .ThenBy(mapObject => mapObject.transform.position.z)
                .ThenBy(mapObject => mapObject.transform.position.y)
                .ToList();

            var result = new List<MapObjectInfoJson>();
            var instanceId = 0;
            foreach (var mapObject in sceneMapObjects)
            {
                // guidは例外を出さずSerializedObject経由の生文字列で検証する
                // Validate the guid via its raw serialized string so no exception is thrown
                var guidString = new SerializedObject(mapObject).FindProperty("mapObjectGuid").stringValue;
                if (!Guid.TryParse(guidString, out _))
                {
                    Debug.LogError($"[MapAuthoring] mapObjectGuidが不正です object:{mapObject.name} guid:'{guidString}'", mapObject);
                    hasError = true;
                    continue;
                }

                // 振り直したIDをシーン側にも書き戻し、シーンと出力を一致させる
                // Write the renumbered id back into the scene so it matches the exported json
                mapObject.SetRuntimeIdentity(instanceId, guidString);
                EditorUtility.SetDirty(mapObject);

                var position = mapObject.transform.position;
                var rotation = mapObject.transform.rotation;
                var scale = mapObject.transform.localScale;
                result.Add(new MapObjectInfoJson
                {
                    InstanceId = instanceId,
                    MapObjectGuidStr = guidString,
                    X = position.x,
                    Y = position.y,
                    Z = position.z,
                    RotationX = rotation.x,
                    RotationY = rotation.y,
                    RotationZ = rotation.z,
                    RotationW = rotation.w,
                    ScaleX = scale.x,
                    ScaleY = scale.y,
                    ScaleZ = scale.z,
                });
                instanceId++;
            }

            return result;
        }

        List<MapVeinInfoJson> BuildMapVeins()
        {
            // item/fluid両authoringを1配列へ集約する。種別はマスタが持つためjsonへは保存しない
            // Merge item/fluid authoring into one array; the type lives in the master, not in the json
            var result = new List<MapVeinInfoJson>();

            foreach (var vein in root.GetComponentsInChildren<ItemMapVeinGameObject>(true))
                AddVein(vein, vein.MinPosition, vein.MaxPosition);
            foreach (var vein in root.GetComponentsInChildren<FluidMapVeinGameObject>(true))
                AddVein(vein, vein.MinPosition, vein.MaxPosition);

            return result;

            void AddVein(MonoBehaviour veinComponent, Vector3Int min, Vector3Int max)
            {
                var guidString = new SerializedObject(veinComponent).FindProperty("veinGuid").stringValue;
                if (!Guid.TryParse(guidString, out _))
                {
                    Debug.LogError($"[MapAuthoring] veinGuidが不正です object:{veinComponent.name} guid:'{guidString}'", veinComponent);
                    hasError = true;
                    return;
                }

                result.Add(new MapVeinInfoJson
                {
                    VeinGuidStr = guidString,
                    MinX = min.x, MinY = min.y, MinZ = min.z,
                    MaxX = max.x, MaxY = max.y, MaxZ = max.z,
                });
            }
        }

        #endregion
    }
}
