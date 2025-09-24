using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class LevelDesignUtil : EditorWindow
    {
        [MenuItem(AltoMenuPath.DevTools + "Level Design Util")]
        static void ShowWindow()
        {
            var window = CreateInstance<LevelDesignUtil>();
            window.titleContent = new GUIContent("LDUtil");
            window.Show();
        }

        void OnGUI()
        {
            if (GUILayout.Button("選択 obj の先頭 2 つ\nを基準に等間隔に整列", GUILayout.MinHeight(40)))
            {
                ArrangeByFirstTwo();
            }
            if (GUILayout.Button("選択 obj をカメラの\n目の前に移動", GUILayout.MinHeight(40)))
            {
                WarpGameObjToFrontOfCamera();
            }
        }

        /// <summary>
        /// 選択したオブジェクトを、選択したもののうち Hierarchy の index の若い 2 つの座標
        /// を基準にして等間隔に並べる。（選択順が取得できなかったのでこんな仕様）
        /// </summary>
        void ArrangeByFirstTwo()
        {
            var objs = Selection.gameObjects.OrderBy(go => go.transform.GetSiblingIndex()).ToList();
            if (objs.Count <= 2)
            {
                Debug.LogError("3 つ以上のオブジェクトを選択してください");
                return;
            }

            var first  = objs[0];
            var second = objs[1];
            Vector3 diff = second.transform.position - first.transform.position;
            for (int i = 2; i < objs.Count; ++i)
            {
                Vector3 pos = first.transform.position + (i * diff);
                Undo.RecordObject(objs[i].transform, "ArrangeByFirstTwo");
                objs[i].transform.position = pos;
            }
        }

        void WarpGameObjToFrontOfCamera()
        {
            var obj = Selection.activeGameObject;
            if (obj == null)
            {
                Debug.LogError("オブジェクトを選択してください");
                return;
            }

            var camera = SceneView.lastActiveSceneView.camera;
            var pos = camera.transform.position + (camera.transform.forward * 5f);
            obj.transform.position = pos;
        }
    }
}
