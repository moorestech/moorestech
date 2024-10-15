using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GearStateChangeProcessor))]
public class GearStateChangeProcessorInspector : Editor
{
    [SerializeField] private float rpm = 60;
    [SerializeField] private bool isClockwise = true;
    private bool isSimulating = false;
    
    private Dictionary<Transform,Quaternion> _initialRotations = new();


    public override void OnInspectorGUI()
    {
        // RPMと回転方向のプロパティ
        // RPM and rotation direction properties
        rpm = EditorGUILayout.FloatField("RPM", rpm);
        isClockwise = EditorGUILayout.Toggle("Is Clockwise", isClockwise);

        var processor = target as GearStateChangeProcessor;
        if (processor == null) return;

        // 現在の状態を表示
        // Display the current state
        if (processor.CurrentGearState != null)
        {
            EditorGUILayout.LabelField("Current Gear State");
            EditorGUILayout.LabelField($"RPM: {processor.CurrentGearState.CurrentRpm}");
            EditorGUILayout.LabelField($"Is Clockwise: {processor.CurrentGearState.IsClockwise}");
        }
        
        // シミュレーション開始/停止ボタン
        // Simulation start/stop button
        if (isSimulating)
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
                isSimulating = false;
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
                isSimulating = true;
                EditorApplication.update += OnEditorUpdate;
            }
        }

        base.OnInspectorGUI();
    }

    private void OnEditorUpdate()
    {
        if (!isSimulating)
        {
            EditorApplication.update -= OnEditorUpdate;
            return;
        }

        var processor = target as GearStateChangeProcessor;
        if (processor != null)
        {
            Rotate(processor);
        }

        // シミュレーション中はInspectorを再描画して変更を反映
        // Repaint the inspector to reflect changes
        Repaint();
    }

    private void Rotate(GearStateChangeProcessor processor)
    {
        var state = new GearStateData(rpm, isClockwise);
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
