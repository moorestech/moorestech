using System.Collections.Generic;
using System.IO;
using Client.Game.Map.MapObject;
using Client.Game.Map.MapVein;
using Game.Map.Interface.Json;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class MapExportAndSetting : EditorWindow
{
    [MenuItem("moorestech/MapExportAndSetting")]
    private static void ShowWindow()
    {
        var window = GetWindow<MapExportAndSetting>();
        window.titleContent = new GUIContent("MapExportAndSetting");
        window.Show();
    }

    private void OnGUI()
    {
        if (!GUILayout.Button("Export and Setting Map"))
        {
            return;
        }

        var mapObjectConfig = new MapInfo
        {
            MapObjects = SetUpMapObjectInfos(),
            MapVeins = GetMapVeinInfo()
        };

        // jsonに変換
        var json = JsonConvert.SerializeObject(mapObjectConfig, Formatting.Indented);

        //ダイアログを出して保存
        var path = EditorUtility.SaveFilePanel("Save map object config", "../../Server/map/", "mapObjects", "json");
        if (path.Length != 0) File.WriteAllText(path, json);

        #region Internal

        List<MapObjectInfos> SetUpMapObjectInfos()
        {
            var datastore = FindObjectOfType<MapObjectGameObjectDatastore>();

            var instanceId = 0;
            var result = new List<MapObjectInfos>();

            foreach (var mapObject in datastore.MapObjects)
            {
                mapObject.SetMapObjectData(instanceId);
                instanceId++;

                var config = new MapObjectInfos
                {
                    Type = mapObject.MapObjectType,
                    InstanceId = mapObject.InstanceId,
                    X = mapObject.GetPosition().x,
                    Y = mapObject.GetPosition().y,
                    Z = mapObject.GetPosition().z
                };
                result.Add(config);
            }

            return result;
        }

        List<MapVeinInfo> GetMapVeinInfo()
        {
            var veins = FindObjectsOfType<MapVeinGameObject>();
            var result = new List<MapVeinInfo>();

            foreach (var vein in veins)
            {
                var config = new MapVeinInfo
                {
                    ItemModId = vein.VeinItemModId,
                    ItemId = vein.VeinItemId,
                    XMin = vein.VeinRangeMinPos.x,
                    YMin = vein.VeinRangeMinPos.y,
                    XMax = vein.VeinRangeMaxPos.x,
                    YMax = vein.VeinRangeMaxPos.y
                };
                result.Add(config);
            }

            return result;
        }

        #endregion
    }
}