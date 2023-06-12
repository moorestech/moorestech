using System.Collections.Generic;
using System.IO;
using Game.MapObject.Interface;
using Game.MapObject.Interface.Json;
using MainGame.Presenter.MapObject;
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

            var configDataList = new List<ConfigMapObjectData>();
            foreach (var stone in datastore.StoneMapObjects)
            {
                var config = new ConfigMapObjectData()
                {
                    Type = stone.MapObjectType,
                    InstanceId = stone.InstanceId,
                    X = stone.GetPosition().x,
                    Y = stone.GetPosition().y,
                    Z = stone.GetPosition().z,
                };
                configDataList.Add(config);
            }

            foreach (var tree in datastore.TreeMapObjects)
            {
                var config = new ConfigMapObjectData()
                {
                    Type = tree.MapObjectType,
                    InstanceId = tree.InstanceId,
                    X = tree.GetPosition().x,
                    Y = tree.GetPosition().y,
                    Z = tree.GetPosition().z,
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