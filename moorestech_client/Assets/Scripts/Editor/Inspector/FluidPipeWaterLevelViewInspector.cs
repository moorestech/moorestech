using Client.Game.InGame.FluidNetwork.View;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FluidPipeWaterLevelView))]
public class FluidPipeWaterLevelViewInspector : Editor
{
    private bool _isSimulating;
    private float _simulatedWaterLevel;

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnDestroy()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        var view = target as FluidPipeWaterLevelView;
        if (!view) return;

        GUILayout.BeginVertical("Editor only info", "window");

        GUILayout.Space(10);

        ShowSimulateButton(view);

        GUILayout.Space(10);

        ShowCurrentWaterLevel(view);

        GUILayout.EndVertical();

        GUILayout.Space(10);

        base.OnInspectorGUI();
    }

    private void ShowSimulateButton(FluidPipeWaterLevelView view)
    {
        if (_isSimulating)
        {
            _simulatedWaterLevel = EditorGUILayout.Slider(_simulatedWaterLevel, 0, 1);

            if (GUILayout.Button("Stop Simulating"))
            {
                view.SetWaterLevel(0);
                _isSimulating = false;
                EditorApplication.update -= OnEditorUpdate;

                Repaint();
            }
        }
        else
        {
            if (GUILayout.Button("Start Simulating"))
            {
                _isSimulating = true;
                EditorApplication.update += OnEditorUpdate;
            }
        }
    }

    private void ShowCurrentWaterLevel(FluidPipeWaterLevelView view)
    {
        var waterLevel = _isSimulating ? _simulatedWaterLevel : view.DebugWaterLevel;
        EditorGUILayout.LabelField(_isSimulating ? "Simulated Water Level" : "Current Water Level", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Fill rate", GUILayout.Width(100));
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, waterLevel, $"{waterLevel:P0}");
        }
    }

    private void OnEditorUpdate()
    {
        var view = target as FluidPipeWaterLevelView;
        if (!view) return;

        view.SetWaterLevel(_simulatedWaterLevel);

        Repaint();
    }
}
