#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewStage : PreviewSceneStage
    {
        private readonly Dictionary<SceneView, ViewState> _views = new();
        private GeneratedMapPreviewRun _run;
        private CancellationTokenSource _cancellation;
        private Scene _mainActiveScene;
        public GeneratedMapPreviewState State { get; private set; } = GeneratedMapPreviewState.Empty;
        public string StatusText { get; private set; } = "生成ボタンでマップを表示します。";

        protected override bool OnOpenStage()
        {
            _mainActiveScene = SceneManager.GetActiveScene();
            if (!base.OnOpenStage())
            {
                Debug.LogError("[GeneratedMapPreview] The preview scene could not be opened.");
                return false;
            }
            foreach (SceneView view in SceneView.sceneViews)
            {
                _views.Add(view, new ViewState(view));
                view.sceneLighting = true;
            }
            // Editorの終了経路をStageの通常クローズへ集約する
            // Route editor lifecycle exits through the ordinary stage-close path
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            EditorApplication.quitting += Close;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            return true;
        }

        protected override GUIContent CreateHeaderContent() => new("Generated Map Preview");

        public void Regenerate()
        {
            if (State is GeneratedMapPreviewState.Generating or GeneratedMapPreviewState.Closing or GeneratedMapPreviewState.Closed)
            {
                Debug.LogWarning($"[GeneratedMapPreview] Regenerate rejected while {State}.");
                return;
            }
            _run?.Dispose();
            _cancellation?.Dispose();
            _run = new GeneratedMapPreviewRun();
            _cancellation = new CancellationTokenSource();
            State = GeneratedMapPreviewState.Generating;
            StatusText = "生成中…";
            GenerateAsync(_run, _cancellation.Token).Forget(OnGenerationException);
        }

        private async UniTask GenerateAsync(GeneratedMapPreviewRun run, CancellationToken cancellationToken)
        {
            try
            {
                await run.ExecuteAsync(scene, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                StatusText = $"期待数: {run.ExpectedMapObjectCount} / 作成数: {run.CreatedMapObjectCount} / 欠損数: {run.MissingMapObjectCount}";
                State = run.MissingMapObjectCount == 0 ? GeneratedMapPreviewState.Ready : GeneratedMapPreviewState.Failed;
                if (State == GeneratedMapPreviewState.Ready) FrameSpawn();
                else
                {
                    Debug.LogError($"[GeneratedMapPreview] Incomplete preview discarded. {StatusText}");
                    run.Dispose();
                }
            }
            finally
            {
                // 閉じたStageをcontinuationで戻さず、例外時だけ再生成可能な失敗へ戻す
                // Never reopen a closed stage from a continuation; only unfinished generation becomes retryable failure
                if (State == GeneratedMapPreviewState.Generating)
                {
                    State = GeneratedMapPreviewState.Failed;
                    StatusText = "生成を完了できませんでした。";
                    run.Dispose();
                }
            }
        }

        private void OnGenerationException(Exception exception)
        {
            if (exception is OperationCanceledException && State is GeneratedMapPreviewState.Closing or GeneratedMapPreviewState.Closed) return;
            if (State == GeneratedMapPreviewState.Failed) StatusText = $"生成失敗: {exception.Message}";
            Debug.LogException(exception);
        }

        public void FrameSpawn()
        {
            if (!CanFrame()) return;
            GetPreviewView().LookAt(_run.SpawnPosition, Quaternion.Euler(45f, -45f, 0f), 30f, false, true);
        }

        public void FrameAll()
        {
            if (!CanFrame()) return;
            GetPreviewView().Frame(_run.TerrainBounds, true);
        }

        private SceneView GetPreviewView()
        {
            // 生成待ちにSceneViewを閉じても表示先を用意し、追加した表示の設定も保存する
            // Ensure a view even if it was closed during generation, preserving newly encountered view settings too
            var view = SceneView.lastActiveSceneView;
            if (view == null) view = EditorWindow.GetWindow<SceneView>();
            if (!_views.ContainsKey(view)) _views.Add(view, new ViewState(view));
            view.sceneLighting = true;
            return view;
        }

        private bool CanFrame()
        {
            if (State == GeneratedMapPreviewState.Ready) return true;
            Debug.LogWarning($"[GeneratedMapPreview] Framing requires Ready; current state is {State}.");
            return false;
        }

        private void Close() => StageUtility.GoToMainStage();
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Close();
        }

        protected override void OnCloseStage()
        {
            State = GeneratedMapPreviewState.Closing;
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            EditorApplication.quitting -= Close;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            try
            {
                // awaitの再開を禁止してから、root・TerrainData・ディスクの順で解放する
                // Cancel resumptions before releasing the root, terrain data, and disk contents
                _cancellation?.Cancel();
                _run?.Dispose();
            }
            finally
            {
                _cancellation?.Dispose();
                foreach (var view in _views.Values) view.Restore();
                _views.Clear();
                if (_mainActiveScene.IsValid() && _mainActiveScene.isLoaded) SceneManager.SetActiveScene(_mainActiveScene);
                base.OnCloseStage();
                State = GeneratedMapPreviewState.Closed;
                StatusText = "プレビューを閉じました。";
            }
        }

        private readonly struct ViewState
        {
            private readonly SceneView _view;
            private readonly Vector3 _pivot;
            private readonly Quaternion _rotation;
            private readonly float _size;
            private readonly bool _orthographic;
            private readonly bool _lighting;

            internal ViewState(SceneView view)
            {
                _view = view;
                _pivot = view.pivot;
                _rotation = view.rotation;
                _size = view.size;
                _orthographic = view.orthographic;
                _lighting = view.sceneLighting;
            }

            internal void Restore()
            {
                // 利用者が閉じたSceneViewは復活させず、残っている表示だけを戻す
                // Restore surviving views without recreating a view the user closed
                if (_view == null) return;
                _view.sceneLighting = _lighting;
                _view.LookAt(_pivot, _rotation, _size, _orthographic, true);
                _view.Repaint();
            }
        }
    }
}
#endif
