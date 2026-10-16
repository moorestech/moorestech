using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        if (_result.Length > 0) EditorGUILayout.HelpBox(_result, _resultType);
        EditorGUILayout.LabelField("Mac mini: scripts/playtest/dev-token.sh issue <機材名>", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(GameSystemPaths.PlaytestDeveloperTokenFilePath, EditorStyles.wordWrappedLabel);
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

    private void SaveToken()
    {
        var token = _input.Trim();
        if (!Regex.IsMatch(token, PlaytestDeveloperTokenFile.TokenPattern))
        {
            SetResult("保存できません: mdt_ + 小文字16進64桁を入力してください。", MessageType.Warning);
            return;
        }
        // ディスクIO境界。保存失敗を画面にも示す
        // Disk IO boundary: expose persistence failures in the window as well
        try
        {
            PlaytestDeveloperTokenFile.Write(GameSystemPaths.PlaytestDeveloperTokenFilePath, token);
            _input = "";
            SetResult("保存しました。", MessageType.Info);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            SetResult($"保存できません: {exception.Message}", MessageType.Error);
        }
        RefreshStatus();
    }

    private void DeleteToken()
    {
        // ディスクIO境界。削除失敗を成功扱いにしない
        // Disk IO boundary: do not report deletion failures as success
        try
        {
            PlaytestDeveloperTokenFile.Delete(GameSystemPaths.PlaytestDeveloperTokenFilePath);
            _input = "";
            SetResult("削除しました。", MessageType.Info);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            SetResult($"削除できません: {exception.Message}", MessageType.Error);
        }
        RefreshStatus();
    }

    private async UniTask CheckConnectionAsync(CancellationToken cancellation)
    {
        RefreshStatus();
        var read = PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath);
        if (!(read is PlaytestDeveloperTokenPresent present))
        {
            SetResult("接続確認できません: 有効なトークンを保存してください。", MessageType.Warning);
            return;
        }
        _checking = true;
        var response = await new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl)
            .PostDeveloperSessionAsync(present.Token, cancellation).SuppressCancellationThrow();
        // 閉じたウィンドウへ非同期結果を反映しない
        // Never apply an asynchronous result to a closed window
        if (response.IsCanceled || cancellation.IsCancellationRequested) return;
        _checking = false;
        if (response.Result.Kind != PlaytestApiResultKind.Responded)
            SetResult($"到達できません: {response.Result.Detail}", MessageType.Error);
        else if (response.Result.StatusCode == 401)
            SetResult("受け口に拒否されました（失効か打ち間違い）", MessageType.Warning);
        else if (response.Result.StatusCode != 200)
            SetResult($"接続確認に失敗しました: HTTP {response.Result.StatusCode}", MessageType.Error);
        else ShowIdentity(response.Result.Body);
    }

    private void ShowIdentity(string body)
    {
        // 外部入力JSON境界。応答破損を画面とログへ残す
        // External JSON boundary: expose malformed responses in both the window and log
        try
        {
            var identity = JObject.Parse(body)["steamId"];
            if (identity?.Type != JTokenType.String || !identity.Value<string>().StartsWith("dev-"))
            {
                SetResult("接続確認に失敗しました: 応答に開発機の steamId がありません。", MessageType.Error);
                return;
            }
            SetResult($"受け口に {identity.Value<string>()} として認識されました", MessageType.Info);
        }
        catch (JsonException exception)
        {
            SetResult($"接続確認に失敗しました: 応答JSONを読めません（{exception.Message}）", MessageType.Error);
        }
    }

    private void SetResult(string message, MessageType type)
    {
        _result = message;
        _resultType = type;
        Debug.Log($"[PlaytestReceiver] {message}");
        Repaint();
    }
}
