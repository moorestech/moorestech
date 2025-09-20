using UnityEngine;
using UnityEditor;

namespace AltoEditor
{
    public class CustomUnityStatsWindow : AltoEditorWindow
    {
        [MenuItem(AltoMenuPath.EditorWindow + "Performance Stats Window")]
        static void ShowWindow()
        {
            GetWindow<CustomUnityStatsWindow>("Stats");
        }

        Vector2 _scrollView;
        bool _autoRefresh = true;
        double _nextRepaintAt;

        //----------------------------------------------------------------------
        // Auto Refresh
        //----------------------------------------------------------------------

        const float RefreshInterval = 0.25f;

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (_autoRefresh && currentTime >= _nextRepaintAt)
            {
                Repaint();
                _nextRepaintAt = currentTime + RefreshInterval;
            }
        }

        //----------------------------------------------------------------------
        // Draw GUI
        //----------------------------------------------------------------------

        void OnGUI()
        {
            _scrollView = GUILayout.BeginScrollView(_scrollView);
            {
                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh");

                this._defaultLabelColor = this.Orange;
                string unitColor = "<color=#77c030>";
                Label("FPS", $"{(1 / UnityStats.frameTime).ToString("F2")}");
                Label("Frame / Render Time", $"{(UnityStats.frameTime * 1000).ToString("F2")} {unitColor}ms</color> / {(UnityStats.renderTime * 1000).ToString("F2")} {unitColor}ms</color>");

                HR();
                Label("Tris / Verts", $"{(UnityStats.triangles / 10000f).ToString("F2")} {unitColor}万</color> / {(UnityStats.vertices / 10000f).ToString("F2")} {unitColor}万</color>");
                Label("Draw Calls", $"{UnityStats.drawCalls}");
                Label("Set Pass Calls", $"{UnityStats.setPassCalls}");

                HR();
                Label("Batches", $"{UnityStats.batches}");
                int savedByBatching = (UnityStats.staticBatchedDrawCalls - UnityStats.staticBatches)
                                    + (UnityStats.dynamicBatchedDrawCalls - UnityStats.dynamicBatches)
                                    + (UnityStats.instancedBatchedDrawCalls - UnityStats.instancedBatches);
                Label("Saved by Batching", $"{savedByBatching}");
                Label("Static Batches / Calls", $"{UnityStats.staticBatches} / {UnityStats.staticBatchedDrawCalls}", LightGray);
                Label("Dynamic Batches / Calls", $"{UnityStats.dynamicBatches} / {UnityStats.dynamicBatchedDrawCalls}", LightGray);
                Label("Instanced Batches / Calls", $"{UnityStats.instancedBatches} / {UnityStats.instancedBatchedDrawCalls}", LightGray);

                HR();
                string resolution = UnityStats.screenRes.Replace("x", " x ");
                Label("Screen Resolution", $"{resolution}");
                Label("Screen Bytes", $"{(UnityStats.screenBytes / 1024f / 1024f).ToString("F2")} {unitColor}MB<color>");
                Label("Texture Bytes", $"{(UnityStats.renderTextureBytes / 1024f / 1024f).ToString("F2")} {unitColor}MB</color>");
                Label("Texture Count / Changes", $"{UnityStats.renderTextureCount} / {UnityStats.renderTextureChanges}");
                Label("Shadow Casters", $"{UnityStats.shadowCasters}");
                Label("Animator Playing", $"{UnityStats.animatorComponentsPlaying}");
                Label("Skinned Meshes", $"{UnityStats.visibleSkinnedMeshes}");
            }
            GUILayout.EndScrollView();
        }
    }
}
