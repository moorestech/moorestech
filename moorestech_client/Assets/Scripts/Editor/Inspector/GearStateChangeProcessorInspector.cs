using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GearStateChangeProcessor))]
public class GearStateChangeProcessorInspector : Editor
{

    public override void OnInspectorGUI()
    {
        var processor = target as GearStateChangeProcessor;
        if (processor == null)
        {
            return;
        }

        GUILayout.BeginVertical("Editor only info", "window");

        ShowCurrentState();

        GUILayout.EndVertical();
        GUILayout.Space(10);

        base.OnInspectorGUI();

        #region Internal

        void ShowCurrentState()
        {
            // 現在の状態を表示
            // Display the current state
            EditorGUILayout.LabelField("Current Gear State", EditorStyles.boldLabel);
            if (processor.DebugCurrentGearState == null)
            {
                EditorGUILayout.LabelField("State data is null");
            }
            else
            {
                EditorGUILayout.LabelField($"RPM: {processor.DebugCurrentGearState.CurrentRpm}");
                EditorGUILayout.LabelField($"Is Clockwise: {processor.DebugCurrentGearState.IsClockwise}");
            }
        }

        #endregion
    }
}
