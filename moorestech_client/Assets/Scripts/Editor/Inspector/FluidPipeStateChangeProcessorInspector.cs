using System.Linq;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Core.Master;
using Game.Fluid;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FluidPipeStateChangeProcessor))]
public class FluidPipeStateChangeProcessorInspector : Editor
{
    private bool _isSimulating;
    private FluidPipeStateDetail _state;
    private float _waterLevel;
    
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
        var processor = target as FluidPipeStateChangeProcessor;
        if (!processor) return;
        
        GUILayout.BeginVertical("Editor only info", "window");
        
        GUILayout.Space(10);
        
        ShowSimulateButton(processor);
        
        GUILayout.Space(10);
        
        ShowCurrentState(processor);
        
        GUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        base.OnInspectorGUI();
    }
    
    private void ShowSimulateButton(FluidPipeStateChangeProcessor processor)
    {
        if (_isSimulating)
        {
            _waterLevel = EditorGUILayout.Slider(_waterLevel, 0, 1);
            
            if (GUILayout.Button("Stop Simulating"))
            {
                processor.UpdateWaterLevel(0);
                processor.ResetMaterial();
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
    
    private void ShowCurrentState(FluidPipeStateChangeProcessor processor)
    {
        var state = processor.DebugCurrentFluidPipeState ?? (_isSimulating ? _state : null);
        
        EditorGUILayout.LabelField(_isSimulating ? "Simulated Fluid Pipe State" : "Current Fluid Pipe State", EditorStyles.boldLabel);
        if (state == null)
        {
            EditorGUILayout.LabelField("State data is null");
        }
        else
        {
            EditorGUILayout.LabelField($"FluidId: {state.FluidId}");
            if (MasterHolder.FluidMaster != null && MasterHolder.FluidMaster.GetAllFluidIds().Contains(state.FluidId))
            {
                var fluidMaster = MasterHolder.FluidMaster.GetFluidMaster(state.FluidId);
                EditorGUILayout.LabelField($"FluidName: {fluidMaster.Name}");
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Amount", GUILayout.Width(100));
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, state.Amount / state.Capacity, $"{state.Amount} / {state.Capacity}");
            }
        }
    }
    
    private void OnEditorUpdate()
    {
        var processor = target as FluidPipeStateChangeProcessor;
        if (!processor) return;
        
        _state = new FluidPipeStateDetail(new FluidId(1), _waterLevel, 1);
        processor.UpdateWaterLevel(_state.Amount / _state.Capacity);
        
        Repaint();
    }
}