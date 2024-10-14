using System.Collections.Generic;
using System.IO;
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
            MapObjects = SetUpMapObjectInfos(),
            MapVeins = GetMapVeinInfo(),
        };
        
        // jsonに変換
        var json = JsonConvert.SerializeObject(mapObjectConfig, Formatting.Indented);
        
        //ダイアログを出して保存
        var path = EditorUtility.SaveFilePanel("Save map object config", "../../Server/map/", "map", "json");
        if (path.Length != 0) File.WriteAllText(path, json);
        
        
        #region Internal
        
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
            var veins = FindObjectsOfType<MapVeinGameObject>();
            var result = new List<MapVeinInfoJson>();
            
            foreach (var vein in veins)
            {
                var config = new MapVeinInfoJson
                {
                    VeinItemGuidStr = vein.VeinItemGuid.ToString(),
                    XMin = vein.VeinRangeMinPos.x,
                    YMin = vein.VeinRangeMinPos.y,
                    XMax = vein.VeinRangeMaxPos.x,
                    YMax = vein.VeinRangeMaxPos.y,
                };
                result.Add(config);
            }
            
            return result;
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