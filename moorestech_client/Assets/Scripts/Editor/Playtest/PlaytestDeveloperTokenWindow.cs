#if UNITY_EDITOR
using System;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEditor;
using UnityEngine;

public sealed class PlaytestDeveloperTokenWindow : EditorWindow
{
    private string _input = "";
    private string _status = "";
    private string _result = "";
    private MessageType _statusType;
    private MessageType _resultType;
    private CancellationTokenSource _lifetime;
    private bool _checking;

    [MenuItem("moorestech/Playtest/Developer Token")]
    private static void ShowWindow()
    {
        var window = GetWindow<PlaytestDeveloperTokenWindow>();
        window.titleContent = new GUIContent("Developer Token");
        window.minSize = new Vector2(520, 260);
        window.Show();
    }

    private void OnEnable()
    {
        _lifetime = new CancellationTokenSource();
        _checking = false;
        RefreshStatus();
    }

    private void OnDisable()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _input = "";
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(_status, _statusType);
        EditorGUILayout.LabelField("開発者トークン");
        _input = EditorGUILayout.PasswordField(_input);
        // 接続中は資格情報を固定して結果との食い違いを防ぐ
        // Keep credentials fixed during a connection check so its result stays relevant
        using (new EditorGUI.DisabledScope(_checking))
        {
            if (GUILayout.Button("保存")) SaveToken();
            if (GUILayout.Button("削除")) DeleteToken();
            if (GUILayout.Button("接続確認")) CheckConnectionAsync(_lifetime.Token).Forget();
        }
        if (_checking) EditorGUILayout.HelpBox("接続確認中…", MessageType.Info);
        if (0 < _result.Length) EditorGUILayout.HelpBox(_result, _resultType);
        EditorGUILayout.LabelField("Mac mini: scripts/playtest/dev-token.sh issue <機材名>", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(GameSystemPaths.PlaytestDeveloperTokenFilePath, EditorStyles.wordWrappedLabel);

        #region Internal

        void SaveToken()
        {
            var result = DeveloperTokenEditorFile.Write(GameSystemPaths.PlaytestDeveloperTokenFilePath, _input);
            if (result.Kind == DeveloperTokenWindowResultKind.Saved) _input = "";
            SetResult(result.Kind, result.Detail);
            RefreshStatus();
        }

        void DeleteToken()
        {
            var result = DeveloperTokenEditorFile.Delete(GameSystemPaths.PlaytestDeveloperTokenFilePath);
            if (result.Kind == DeveloperTokenWindowResultKind.Deleted) _input = "";
            SetResult(result.Kind, result.Detail);
            RefreshStatus();
        }

        async UniTask CheckConnectionAsync(CancellationToken cancellation)
        {
            RefreshStatus();
            var read = PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath);
            if (!(read is PlaytestDeveloperTokenPresent present))
            {
                SetResult(DeveloperTokenWindowResultKind.CredentialRequired, "");
                return;
            }
            _checking = true;
            var session = new PlaytestSession(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), new DeveloperTokenSessionCredential(present.Token));
            var response = await session.AuthenticateAsync(DateTime.UtcNow, cancellation).SuppressCancellationThrow();
            // silent-ok: 閉じた画面には結果を反映しない / 次の接続確認時に状態を再読取する
            if (response.IsCanceled || cancellation.IsCancellationRequested) return;
            _checking = false;
            // 実送信と同じ検証済み結果を表示する
            // Display the same validated result used for production delivery
            if (response.Result is PlaytestSessionAuthenticated authenticated)
                SetResult(DeveloperTokenWindowResultKind.Connected, authenticated.Response.SteamId);
            else
            {
                var failed = (PlaytestSessionFailed)response.Result;
                var kind = failed.Outcome switch
                {
                    PlaytestSessionOutcome.TicketRejected => DeveloperTokenWindowResultKind.Refused,
                    PlaytestSessionOutcome.MalformedResponse => DeveloperTokenWindowResultKind.MalformedResponse,
                    _ => DeveloperTokenWindowResultKind.Unreachable,
                };
                SetResult(kind, failed.Detail);
            }
        }

        #endregion
    }

    private void RefreshStatus()
    {
        // 読取りは描画毎に繰り返さず、操作の区切りで更新する
        // Refresh at operation boundaries rather than reading the disk on every repaint
        var read = PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath);
        _statusType = MessageType.Info;
        switch (read)
        {
            case PlaytestDeveloperTokenPresent present:
                _status = $"設定済み（mdt_…{present.Token.Substring(present.Token.Length - 6)}）";
                break;
            case PlaytestDeveloperTokenUnreadable unreadable:
                _status = $"読めない: {unreadable.Reason}";
                _statusType = MessageType.Error;
                break;
            default:
                _status = "未設定";
                break;
        }
    }

    private void SetResult(DeveloperTokenWindowResultKind kind, string detail)
    {
        // 結果別に表示・ログ重要度を統一
        // Derive presentation and log severity together from the fixed result
        (_result, _resultType) = kind switch
        {
            DeveloperTokenWindowResultKind.InvalidToken => ("保存できません: mdt_ + 小文字16進64桁を入力してください。", MessageType.Warning),
            DeveloperTokenWindowResultKind.Saved => ("保存しました。", MessageType.Info),
            DeveloperTokenWindowResultKind.SaveFailed => ($"保存できません: {detail}", MessageType.Error),
            DeveloperTokenWindowResultKind.Deleted => ("削除しました。", MessageType.Info),
            DeveloperTokenWindowResultKind.DeleteFailed => ($"削除できません: {detail}", MessageType.Error),
            DeveloperTokenWindowResultKind.CredentialRequired => ("接続確認できません: 有効なトークンを保存してください。", MessageType.Warning),
            DeveloperTokenWindowResultKind.Unreachable => ($"到達できません: {detail}", MessageType.Error),
            DeveloperTokenWindowResultKind.Refused => ("受け口に拒否されました（失効か打ち間違い）", MessageType.Warning),
            DeveloperTokenWindowResultKind.MalformedResponse => ("接続確認に失敗しました: 開発機セッション応答が不正です。", MessageType.Error),
            DeveloperTokenWindowResultKind.Connected => ($"受け口に {detail} として認識されました", MessageType.Info),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown developer token result"),
        };
        if (_resultType == MessageType.Error) Debug.LogError($"[PlaytestReceiver] {_result}");
        else if (_resultType == MessageType.Warning) Debug.LogWarning($"[PlaytestReceiver] {_result}");
        Repaint();
    }
}
#endif
