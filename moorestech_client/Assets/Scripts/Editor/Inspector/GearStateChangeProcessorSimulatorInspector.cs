using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GearStateChangeProcessorSimulator))]
public class GearStateChangeProcessorSimulatorInspector : Editor
{
    private SerializedProperty _targetProcessorProp;
    private SerializedProperty _isSimulatingProp;
    private SerializedProperty _simulateRpmProp;
    private SerializedProperty _simulateIsClockwiseProp;

    private void OnEnable()
    {
        // SerializedPropertyの取得
        // Get SerializedProperties
        _targetProcessorProp = serializedObject.FindProperty("targetProcessor");
        _isSimulatingProp = serializedObject.FindProperty("isSimulating");
        _simulateRpmProp = serializedObject.FindProperty("simulateRpm");
        _simulateIsClockwiseProp = serializedObject.FindProperty("simulateIsClockwise");
    }

    public override void OnInspectorGUI()
    {
        var simulator = target as GearStateChangeProcessorSimulator;
        if (simulator == null) return;

        serializedObject.Update();

        GUILayout.BeginVertical("Gear Simulator Settings", "window");

        // ターゲットプロセッサーの設定
        // Target processor settings
        EditorGUILayout.PropertyField(_targetProcessorProp, new GUIContent("Target Processor"));

        GUILayout.Space(10);

        // シミュレーション設定
        // Simulation settings
        EditorGUILayout.LabelField("Simulation Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_simulateRpmProp, new GUIContent("RPM"));
        EditorGUILayout.PropertyField(_simulateIsClockwiseProp, new GUIContent("Is Clockwise"));

        GUILayout.Space(10);

        // シミュレーションON/OFFトグル
        // Simulation ON/OFF toggle
        var wasSimulating = _isSimulatingProp.boolValue;
        EditorGUILayout.PropertyField(_isSimulatingProp, new GUIContent("Is Simulating"));
        var isSimulating = _isSimulatingProp.boolValue;

        // トグルの状態が変わった場合の処理
        // Process when toggle state changes
        if (wasSimulating != isSimulating)
        {
            if (isSimulating)
            {
                simulator.StartSimulation();
            }
            else
            {
                simulator.StopSimulation();
            }
        }

        GUILayout.Space(10);

        // 現在の状態を表示
        // Display current state
        ShowCurrentState(simulator);

        GUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();

        #region Internal

        void ShowCurrentState(GearStateChangeProcessorSimulator sim)
        {
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);

            if (sim.TargetProcessor == null)
            {
                EditorGUILayout.LabelField("Target Processor is not set");
                return;
            }

            if (sim.TargetProcessor.DebugCurrentGearState == null)
            {
                EditorGUILayout.LabelField("State data is null");
            }
            else
            {
                var state = sim.TargetProcessor.DebugCurrentGearState;
                EditorGUILayout.LabelField($"RPM: {state.CurrentRpm}");
                EditorGUILayout.LabelField($"Is Clockwise: {state.IsClockwise}");
            }
        }

        #endregion
    }
}
