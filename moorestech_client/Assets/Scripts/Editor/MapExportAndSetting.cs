using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.MapVein;
using Game.Map.Interface.Json;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class MapExportAndSetting : EditorWindow
{
    private void OnGUI()
    {
        if (!GUILayout.Button("Export and Setting Map")) return;
        
        var mapObjectConfig = new MapInfoJson
        {
            DefaultSpawnPointJson = GetSpawnPointJson(),
            MapObjects = SetUpMapObjectInfos(),
            MapVeins = GetMapVeinInfo(),
        };
        
        // jsonに変換
        var json = JsonConvert.SerializeObject(mapObjectConfig, Formatting.Indented);
        
        //ダイアログを出して保存
        var path = EditorUtility.SaveFilePanel("Save map object config", "../../Server/map/", "map", "json");
        if (path.Length != 0) File.WriteAllText(path, json);
        
        
        #region Internal
        
        SpawnPointJson GetSpawnPointJson()
        {
            var spawnPoint = FindObjectsByType<SpawnPointObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            
            if (spawnPoint == null)
            {
                Debug.LogError("MapSpawnPoint not found in the scene.");
                return new SpawnPointJson {X = 0, Y = 0, Z = 0,};
            }
            
            var point = spawnPoint.transform.position;
            return new SpawnPointJson
            {
                X = point.x,
                Y = point.y,
                Z = point.z,
            };
        }
        
        List<MapObjectInfoJson> SetUpMapObjectInfos()
        {
            var datastore = FindObjectOfType<MapObjectGameObjectDatastore>();
            datastore.FindMapObjects();
            EditorUtility.SetDirty(datastore);
            
            var instanceId = 0;
            var result = new List<MapObjectInfoJson>();
            
            foreach (var mapObject in datastore.MapObjects)
            {
                mapObject.SetMapObjectData(instanceId);
                instanceId++;
                
                var config = new MapObjectInfoJson
                {
                    MapObjectGuidStr = mapObject.MapObjectGuid.ToString(),
                    InstanceId = mapObject.InstanceId,
                    X = mapObject.GetPosition().x,
                    Y = mapObject.GetPosition().y,
                    Z = mapObject.GetPosition().z,
                };
                result.Add(config);
            }
            
            return result;
        }
        
        List<MapVeinInfoJson> GetMapVeinInfo()
        {
            // item/fluid両authoringを1配列へ集約。veinGuidのみ書き出し、種別はマスタが持つ
            // Merge item/fluid authoring into one array; write veinGuid only, type lives in master
            var result = new List<MapVeinInfoJson>();

            foreach (var vein in FindObjectsOfType<ItemMapVeinGameObject>(true))
                result.Add(BuildVeinInfo(vein.VeinGuid.ToString(), vein.MinPosition, vein.MaxPosition));

            foreach (var vein in FindObjectsOfType<FluidMapVeinGameObject>(true))
                result.Add(BuildVeinInfo(vein.VeinGuid.ToString(), vein.MinPosition, vein.MaxPosition));

            return result;
        }

        MapVeinInfoJson BuildVeinInfo(string veinGuid, Vector3Int min, Vector3Int max)
        {
            return new MapVeinInfoJson
            {
                VeinGuidStr = veinGuid,
                MinX = min.x,
                MinY = min.y,
                MinZ = min.z,
                MaxX = max.x,
                MaxY = max.y,
                MaxZ = max.z,
            };
        }

        #endregion
    }
    
    [MenuItem("moorestech/MapExportAndSetting")]
    private static void ShowWindow()
    {
        var window = GetWindow<MapExportAndSetting>();
        window.titleContent = new GUIContent("MapExportAndSetting");
        window.Show();
    }
}