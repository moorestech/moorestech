using System.Collections.Generic;
using System.IO;
using Game.MapObject.Interface;
using Game.MapObject.Interface.Json;
using MainGame.Presenter.MapObject;
using MainGame.UnityView.MapObject;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MainGame.Editor
{
    [CustomEditor(typeof(MapObjectGameObjectDatastore))]  
    public class MapObjectGameObjectDatastoreEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("マップオブジェクトの設定と\nサーバー向けコンフィグファイル生成"))
            {
                SetUpMapObject();
            }
            base.OnInspectorGUI();
        }

        private void SetUpMapObject()
        {
            var datastore = target as MapObjectGameObjectDatastore;

            var instanceId = 0;
            foreach (var stone in datastore.StoneMapObjects)
            {
                stone.SetMapObjectData(instanceId,VanillaMapObjectType.VanillaStone);
                instanceId++;
            }
            foreach (var tree in datastore.TreeMapObjects)
            {
                tree.SetMapObjectData(instanceId,VanillaMapObjectType.VanillaTree);
                instanceId++;
            }
            foreach (var bush in datastore.BushMapObjects)
            {
                bush.SetMapObjectData(instanceId,VanillaMapObjectType.VanillaBush);
                instanceId++;
            }

            var configDataList = new List<ConfigMapObjectData>();
            var allDatastore = new List<MapObjectGameObject>();
            allDatastore.AddRange(datastore.StoneMapObjects);
            allDatastore.AddRange(datastore.TreeMapObjects);
            allDatastore.AddRange(datastore.BushMapObjects);
            foreach (var mapObject in allDatastore)
            {
                var config = new ConfigMapObjectData()
                {
                    Type = mapObject.MapObjectType,
                    InstanceId = mapObject.InstanceId,
                    X = mapObject.GetPosition().x,
                    Y = mapObject.GetPosition().y,
                    Z = mapObject.GetPosition().z,
                };
                configDataList.Add(config);
            }


            var mapObjectConfig = new ConfigMapObjects()
            {
                MapObjects = configDataList.ToArray()
            };

            // jsonに変換
            var json = JsonConvert.SerializeObject(mapObjectConfig, Formatting.Indented);
            
            //ダイアログを出して保存
            var path = EditorUtility.SaveFilePanel("Save map object config", "", "mapObjects", "json");
            if (path.Length != 0)
            {
                File.WriteAllText(path, json);
            }
        }
    }
}