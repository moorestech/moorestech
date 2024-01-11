using System.Collections.Generic;
using System.IO;
using Game.MapObject.Interface.Json;
using MainGame.Presenter.MapObject;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapObjectGameObjectDatastore))]
public class MapObjectGameObjectDatastoreEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("マップオブジェクトの設定と\nサーバー向けコンフィグファイル生成")) SetUpMapObject();
        base.OnInspectorGUI();
    }

    private void SetUpMapObject()
    {
        var datastore = target as MapObjectGameObjectDatastore;

        var instanceId = 0;
        var configDataList = new List<ConfigMapObjectData>();

        foreach (var mapObject in datastore.MapObjects)
        {
            Debug.Log(mapObject.MapObjectType);
        }


        var mapObjectConfig = new ConfigMapObjects
        {
            MapObjects = configDataList.ToArray()
        };

        // jsonに変換
        var json = JsonConvert.SerializeObject(mapObjectConfig, Formatting.Indented);

        //ダイアログを出して保存
        var path = EditorUtility.SaveFilePanel("Save map object config", "../../Server/map/", "mapObjects", "json");
        if (path.Length != 0) File.WriteAllText(path, json);
    }
}