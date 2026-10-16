# Task10 UI scout

静的調査のみ。Unity実行・受け口操作・トークン読取はしていない。plan全体は未読。

## 再利用する実在シナリオ/API

- `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/pause-menu-hierarchy.cs` が報告画面・文字入力・送信を実装済み。列車部分を除いた前半が本件の雛形。
- `PlaytestRunner.Run(name, new PlaytestRunOptions { Record = true }, async p => ...)`、`p.SetupDebugEnvironment(new PlaytestEnvironmentConfig())`、`p.SkipOpeningSkit()` を使用。
- 既存シナリオ冒頭はGameViewのdock selectedを変え、Recorder初回フレーム待ちによるtimeScale=0を避ける。録画時はこの対策を保持。
- `ClientDIContext.DIContainer.DIContainerResolver.Resolve<PauseMenuStateService>()`、`Resolve<BugReportCaptureSession>()` が実在。
- `p.PressKey(Key.Escape)` → `p.WaitUiState(UIStateEnum.PauseMenu, 15f)` → `p.ClickWebUi("pause-menu-open-bug-report")`。
- `pauseMenu.CurrentPage.Value == PauseMenuPage.BugReport`、`captureSession.Status.Value.Kind == BugReportCaptureStatus.Ready` が実在する待機条件。
- テキスト入力は `p.ClickWebUi("bug-report-description")` → `CefScreenMapper.TryGetBrowser(out var browser)` →各文字 `browser.SendCharEvent(character, 0)` と `await UniTask.Yield()`。既存シナリオと同じ経路。
- `p.ClickWebUi("bug-report-send")` が送信操作。`BugReportSubmitActionHandler.ExecuteAsync` が成功すると `PauseMenuPage.Top` へ戻す。完了状態を撮るには報告画面を再度開く。
- 表示確認は `PlaytestWebUiOps.WaitWebUiTextContains("bug-report-delivery", expected, 15f)`。実装は `Client.Playtest/Operations/PlaytestWebUiOps.cs`。表示要素は `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx:78`。
- 言語設定はトップから `pause-menu-open-settings` → `language-select-option-japanese` → `pause-menu-locale-japanese` を待つ →Escapeでトップ。
- GameScreenStateの直接キー読み取りはHybridInput。CEFクリックはPlaytestRunnerがCefInputForwarderを起動しInputSystem注入を転送する。Escapeの具体的読取箇所は今回の検索では特定していないが、既存シナリオに同じEscape操作がある。実機成功は未確認。
- 本件は建設・接続なし。connector offset/座標計算は対象なし。

## 送信状態の実在名

`Client.PlaytestReceiver.Delivery.PlaytestDeliveryStatusBoard.Current.Value`:
- `PlaytestDeliveryLocalOnly` (`Reason`, `PendingCount`)
- `PlaytestDeliveryPending` (`PendingCount`)
- `PlaytestDeliveryRefused` (`PendingCount`)
- `PlaytestDeliveryAllSent`

UI DTO kind は localOnly/pending/refused/allSent。機材共有outboxは `GameSystemPaths.BugReportOutboxDirectory`、progressは `GameSystemPaths.ProgressRecordOutboxDirectory`。
READY + manifest存在の新規ディレクトリを実行前後の差分で特定し、manifest.descriptionを今回固有の文字列と照合する。UPLOADED存在を個別箱で待つ。

## Token Windowの実在フィールド/手続き

`Assets/Scripts/Editor/Playtest/PlaytestDeveloperTokenWindow.cs` はglobal namespaceのEditorWindow。
- メニュー `moorestech/Playtest/Developer Token`。`EditorApplication.ExecuteMenuItem` で開く。
- private instance fields `_input`, `_status`, `_result`, `_checking`, `_lifetime`。
- private instance `SaveToken()` / `DeleteToken()` / `CheckConnectionAsync(CancellationToken)`。
- EDCで `EditorWindow.GetWindow<PlaytestDeveloperTokenWindow>()`、BindingFlags.Instance|NonPublicで `_input` に一時ファイル内容を代入、`SaveToken` をInvoke。
- 保存成功は `_input` が空、`_result == "保存しました。"`、`_status` が設定済み。値は返却・ログ出力しない。一時ファイルを削除してウィンドウ全体をスクショ。
- 接続確認は反射Invoke戻り値を `UniTask` にcastしてawait可能。CancellationToken.Noneで呼べるが、通常UI同様に `_lifetime` のCancellationTokenSource.Tokenを渡すのが自然。
- 成功時 `_result == "受け口に dev-macmini として認識されました"`。この文字列だけを観測出力する。

## 実行順

1. 停止状態でトークン未設定を確保（既存トークンを消す場合は安全に退避して最後に復元/新しいmacminiトークンを常設）。他worktreeも同ファイルを読む点を記録。
2. Record=falseの探索ランで起動→スキットスキップ→日本語→ポーズ→報告画面。LocalOnly/DeveloperTokenMissingとDOM文言を確認。探索後停止。
3. Record=trueで同じ操作を行い「未送信: 開発者トークンが未設定です」と件数をスクショ。報告1件をローカル作成してREADY・未UPLOADEDのIDを保持。停止。
4. 停止状態のToken Windowで保存・接続確認・スクショ。秘密値は一時ファイル経由のみ。
5. 再生でMainGameの `PlaytestBootUploadTrigger.Start()` が `IPlaytestUploadRequester.RequestUpload()` を呼ぶため、前回ローカル箱のUPLOADEDを待つ。録画上はポーズ→報告ページでallSentを確認。
6. さらに新しい報告をUIから送信し、トップへ戻った後、報告ページを再度開く。PendingとAllSentの型遷移を記録し、DOM「送信待ち」「すべての報告を送信しました」を撮る。ネットワークが速いとpending表示を再度開く前に通過する可能性がある。通過した場合は『pendingを目視済み』としない。
7. 停止後再送は、トークンなしで作った箱の次回送信だけでは『送信中断からの再送』の証明にならない。別ランでREADYかつ未UPLOADED・Pendingを観測したIDを保持して即停止し、次回再生で同じIDのUPLOADEDを確認する。送信完了との競合があるため、停止後も未UPLOADEDを確認できた試行だけ採用。大きい実報告箱などで時間窓を作る場合も、単にフラグを書き換えた実験を本番中断の証明にしない。
8. ingest後、同IDのdev-macmini格納先・manifest・repo/bundle-status.txt・commits.bundle・logs・ingest.jsonを確認。
9. 実在progress READY箱のIDと未UPLOADEDを前後比較する。開発者経路はReportsOnlyで `PlaytestOutboxScanner` がprogressを列挙しない。受け口inboxにも当該progress/dev-macminiが無いことを調べる。『progress箱自体が無い』状態は送られなかった証明として弱い。
10. 提出版はRecord=trueの動画非0byte、result.json Success/Asserts、GameView実プレイ視点、状態スクショを揃える。テスト実行と録画は並走しない。

`PlaytestUploadRunner.ResetOnPlayMode()` がSubsystemRegistrationでstatic走行/資格情報を初期化し、UploadPendingAsyncにはApplication.exitCancellationTokenを渡す。停止再送はこの経路を実機で検証する。

## 配布版smokeの既存手順

`scripts/playtest/README.md:133` 以降。`scripts/playtest/verify-on-windows.sh <steamBuildLabel>` が入口。内部 `windows/run-smoke.ps1` がSteam経由でphase1/phase2を起動。phase1は新規ワールド/チュートリアル/セーブ、phase2はロード/報告送信/UPLOADED待ち。
`steam.exe -applaunch 1958160 --playtestSmoke --smokePhase <phase> --smokeResultDirectory <dir>` を使い、exe直接起動しない。結果はresult.json.success。受け口の `/v1/inbox/report/<steamId>/<id>/READY` で到達しACKする。
Steamログイン・インストール・同意済み・EventModeなしが前提。配布版smokeのWindows機材とEditor検証用winbuildは同一と決めつけない。既存READMEは検証中ingest停止を要求し、サービス操作前にはHERMES-OPS読取が必要。
既存smokeはSteam配布ビルドに対する検証で、現在ブランチのEditor合格の代替ではない。今回変更を含む配布ビルドと対象labelが無ければ今回変更の配布版検証としては未実施と記録する。
