using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AltoEditor
{
    public class GameObjectArranger : EditorWindow
    {
        protected GameObject _parent;
        protected GameObject _prefab;
        protected Vector3 _originPos        = Vector3.zero;
        protected Vector3 _generateNum      = Vector3.one;
        protected Vector3 _interval         = Vector3.one;
        protected Vector3 _posScatter       = Vector3.zero;
        protected Vector3 _posOffsetByScale = Vector3.zero;
        protected Vector2 _scaleRangeX      = Vector2.one;
        protected Vector2 _scaleRangeY      = Vector2.one;
        protected Vector2 _scaleRangeZ      = Vector2.one;
        protected Vector2 _rotRangeX        = Vector2.zero;
        protected Vector2 _rotRangeY        = Vector2.zero;
        protected Vector2 _rotRangeZ        = Vector2.zero;

        protected int MaxNum => 100;  // for safety

        protected class GenerateInfo
        {
            public Vector3 pos   = Vector3.zero;
            public Vector3 scale = Vector3.one;
            public Vector3 rot   = Vector3.zero;
            public bool ignore = false;
        }

        protected Vector2 _scrollPos = Vector2.zero;

        [MenuItem(AltoMenuPath.DevTools + "GameObject Arranger")]
        static void ShowWindow()
        {
            var window = CreateInstance<GameObjectArranger>();
            window.titleContent = new GUIContent("GameObj Arranger");
            window.Show();
        }

        void OnSelectionChange()
        {
            if (Selection.gameObjects.Length > 0)
            {
                _prefab = Selection.gameObjects[0];
                Repaint();
            }
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space();
            _parent = EditorGUILayout.ObjectField("Parent Object", _parent, typeof(GameObject), true) as GameObject;
            _prefab = EditorGUILayout.ObjectField("Generate Target", _prefab, typeof(GameObject), true) as GameObject;

            _originPos = EditorGUILayout.Vector3Field("Origin Pos", _originPos);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                _generateNum = EditorGUILayout.Vector3Field("Number to Generate", _generateNum);
            }
            EditorGUILayout.EndVertical();

            _interval         = EditorGUILayout.Vector3Field("Interval", _interval);
            _posScatter       = EditorGUILayout.Vector3Field("Pos Scatter", _posScatter);
            _posOffsetByScale = EditorGUILayout.Vector3Field("Pos Offset by scale", _posOffsetByScale);

            EditorGUILayout.Space();
            GUILayout.Label("Random Range", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Scale Range (X, Y, Z)");
                _scaleRangeX = EditorGUILayout.Vector2Field("", _scaleRangeX);
                _scaleRangeY = EditorGUILayout.Vector2Field("", _scaleRangeY);
                _scaleRangeZ = EditorGUILayout.Vector2Field("", _scaleRangeZ);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Euler Angle Range (X, Y, Z)");
                _rotRangeX = EditorGUILayout.Vector2Field("", _rotRangeX);
                _rotRangeY = EditorGUILayout.Vector2Field("", _rotRangeY);
                _rotRangeZ = EditorGUILayout.Vector2Field("", _rotRangeZ);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20f);
            if (GUILayout.Button("Generate"))
            {
                GenerateObjects();
            }
            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }

        void GenerateObjects()
        {
            if (_prefab == null)
            {
                Debug.LogWarning("Please select generate target object.");
                return;
            }
            if (_parent == _prefab)
            {
                Debug.LogWarning("Parent object and Target object should be different.");
                return;
            }

            int numX = Math.Min(Math.Max((int)_generateNum.x, 1), MaxNum);
            int numY = Math.Min(Math.Max((int)_generateNum.y, 1), MaxNum);
            int numZ = Math.Min(Math.Max((int)_generateNum.z, 1), MaxNum);

            var genInfo = new List<GenerateInfo>();
            for (int z = 0; z < numZ; ++z)
            {
                for (int y = 0; y < numY; ++y)
                {
                    for (int x = 0; x < numX; ++x)
                    {
                        genInfo.Add(MakeGenerateInfo(x, y, z));
                    }
                }
            }

            Debug.Log($"Generate { genInfo.Count } objects ...");
            PreprocessGenerateInfo(genInfo);
            DoGenerate(genInfo);
        }

        GenerateInfo MakeGenerateInfo(int x, int y, int z)
        {
            Vector3 scale = new Vector3(
                Random.Range(_scaleRangeX.x, _scaleRangeX.y),
                Random.Range(_scaleRangeY.x, _scaleRangeY.y),
                Random.Range(_scaleRangeZ.x, _scaleRangeZ.y)
            );
            Vector3 rot = new Vector3(
                Random.Range(_rotRangeX.x, _rotRangeX.y),
                Random.Range(_rotRangeY.x, _rotRangeY.y),
                Random.Range(_rotRangeZ.x, _rotRangeZ.y)
            );

            float offsetX = _posOffsetByScale.x * scale.x;
            float offsetY = _posOffsetByScale.y * scale.y;
            float offsetZ = _posOffsetByScale.z * scale.z;
            offsetX += Random.Range(0, _posScatter.x) - _posScatter.x / 2;
            offsetY += Random.Range(0, _posScatter.y) - _posScatter.y / 2;
            offsetZ += Random.Range(0, _posScatter.z) - _posScatter.z / 2;
            Vector3 pos = new Vector3(
                _originPos.x + (x * _interval.x) + offsetX,
                _originPos.y + (y * _interval.y) + offsetY,
                _originPos.z + (z * _interval.z) + offsetZ
            );
            return new GenerateInfo{ pos = pos, scale = scale, rot = rot };
        }

        protected virtual void PreprocessGenerateInfo(List<GenerateInfo> genInfo)
        {
            // Implement in subclasses.
        }

        protected virtual void DoGenerate(List<GenerateInfo> genInfo)
        {
            int count = 0;
            foreach (var info in genInfo)
            {
                if (info.ignore) { continue; }

                GameObject obj = Instantiate(_prefab, info.pos, Quaternion.identity);
                ++count;
                obj.name = $"{ _prefab.name }-{ count }";

                if (_parent != null)
                {
                    obj.transform.SetParent(_parent.transform);
                }
                obj.transform.localScale  = info.scale;
                obj.transform.eulerAngles = info.rot;
                Undo.RegisterCreatedObjectUndo(obj, "Alto-Object-Arrange");
            }
        }
    }
}
