using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GearStateChangeProcessor))]
public class GearStateChangeProcessorInspector : Editor
{
    [SerializeField] private float simulateRpm = 60;
    [SerializeField] private bool simulateIsClockwise = true;
    private bool _isSimulating = false;
    
    private readonly Dictionary<Transform, Quaternion> _initialRotations = new();

    public override void OnInspectorGUI()
    {
        var processor = target as GearStateChangeProcessor;
        if (processor == null)
        {
            return;
        }
        
        GUILayout.BeginVertical("Editor only info", "window");
        
        EditorGUILayout.LabelField("Simulate Gear State", EditorStyles.boldLabel);
        ShowSimulateProperty();
        ShowSimulateButton();
        
        GUILayout.Space(10);
        
        ShowCurrentState();

        GUILayout.EndVertical();
        GUILayout.Space(10);

        base.OnInspectorGUI();
        
        #region Internal
        
        void ShowSimulateProperty()
        {
            // RPMと回転方向のプロパティ
            // RPM and rotation direction properties
            simulateRpm = EditorGUILayout.FloatField("RPM", simulateRpm);
            simulateIsClockwise = EditorGUILayout.Toggle("Is Clockwise", simulateIsClockwise);
        }
        
        void ShowSimulateButton()
        {
            // シミュレーション開始/停止ボタン
            // Simulation start/stop button
            if (_isSimulating)
            {
                if (GUILayout.Button("Stop Simulate"))
                {
                    foreach (var rotationInfo in processor.RotationInfos)
                    {
                        if (_initialRotations.TryGetValue(rotationInfo.RotationTransform, out var initialRotation))
                        {
                            rotationInfo.RotationTransform.rotation = initialRotation;
                        }
                    }
                    _isSimulating = false;
                    EditorApplication.update -= OnEditorUpdate;
                }
            }
            else
            {
                if (GUILayout.Button("Start Simulate"))
                {
                    _initialRotations.Clear();
                    foreach (var rotationInfo in processor.RotationInfos)
                    {
                        _initialRotations.Add(rotationInfo.RotationTransform, rotationInfo.RotationTransform.rotation);
                    }
                    _isSimulating = true;
                    EditorApplication.update += OnEditorUpdate;
                }
            }
        }
        
        void ShowCurrentState()
        {
            // 現在の状態を表示
            // Display the current state
            EditorGUILayout.LabelField("Current Gear State", EditorStyles.boldLabel);
            if (processor.CurrentGearState == null)
            {
                EditorGUILayout.LabelField("State data is null");
            }
            else
            {
                EditorGUILayout.LabelField($"RPM: {processor.CurrentGearState.CurrentRpm}");
                EditorGUILayout.LabelField($"Is Clockwise: {processor.CurrentGearState.IsClockwise}");
            }
        }
        
        #endregion
    }

    private void OnEditorUpdate()
    {
        if (!_isSimulating)
        {
            EditorApplication.update -= OnEditorUpdate;
            return;
        }

        var processor = target as GearStateChangeProcessor;
        if (processor != null)
        {
            Rotate(processor);
        }

        // Repaint the inspector to reflect changes
        Repaint();
    }

    private void Rotate(GearStateChangeProcessor processor)
    {
        var state = new GearStateDetail(simulateIsClockwise, simulateRpm, 0, GearNetworkInfo.CreateEmpty());
        processor.Rotate(state);
    }
    
    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }
    private void OnDestroy()
    {
        EditorApplication.update -= OnEditorUpdate;
    }
}
