# 0065. プレイテストの識別は受け口の検証済みSteamIDで確定し、参加同意と前回異常終了の確認はタイトルで出す

日付: 2026-09-20
状態: 採択（ADR 0060 裁定4 の差込口 `PlaytestSessionIdentityProvider` へ実際に値を入れる経路を定める。ADR 0061 の「同意は初回起動時の表示で取る」「クラッシュはタイトルで送信確認を出す」の表示場所を、実装が置いていた Play locally 後のワールド初期化からタイトルへ移す）

## Context

2026-09-19 の検証機 smoke（`playtest-20260919-1727`）後の調査で2件が判明した。

1. **SteamID が記録に入らない（bd `moorestech-vv5l`）。** `PlaytestSessionIdentityProvider.SetCurrent` が本番コードから一度も呼ばれておらず、進行記録のヘッダ・バグ報告 manifest・異常終了箱（`CleanExitMarkWriter` → `SessionOriginSnapshot` → `CrashBundleWriter`）の `steamId` が常に null になる。受け口 `/v1/session` は Steam Web API チケットを検証したうえで `{ steamId, allowed, token, expiresAt }` を返している（`tools/playtest-receiver/src/routes/session.ts:54`）が、クライアントの `PlaytestSessionResponse` は `steamId` を読んでいない。アップロードの置き場所は token の steamId で決まる（`progress/{steamId}/…`）ため、誰の記録かは失われていない。smoke の `result.json` で SteamID が見えていたのは、smoke 専用コードが `SteamUser.GetSteamID()` を直接読んでいたからである。
2. **参加同意画面でクリックが効かない（9/18 の検証機で観測）。** 同意画面は WebUI（CEF）で、Play locally 後の `MainGameInitializationFinalizer.FinalizeAsync` 内で表示される。同じタイミングで MainGame シーンの `GameStateController.Start` が `InputManager.MouseCursorVisible(false)` を呼び、カーソルが画面中央にロックされる。CEF へのクリックは `Input.mousePosition` から座標変換して転送されるため、ロック中はどこを押しても中央のクリックになる（コードの順序は確認済み、検証機での実証は未）。前セッションは原因を「CEF の raw input 奪取」と記録したが裏付けは無い。
   あわせて 2026-09-18 に「参加同意はワールドに依らない参加条件なのでタイトルで出す」と裁定済み（bd `moorestech-odw8`）。現状はワールドのロードが失敗すると（`moorestech-pv0j`）同意に到達できない。

タイトル（MainMenu シーン）は uGUI で、WebUiHost は Play locally 後の `InitializeScenePipeline` で初めて起動する。uGUI は 2026-09-05 に「パッケージごと完全撤去（MainMenu を含む）」と裁定済みで、メインメニューの作り変えは別タスク（bd `moorestech-zohw`）になっている。

## Decision

- **報告・進行記録・異常終了箱に入れる SteamID は、起動時照合で受け口が返した検証済みの `steamId` とする。** 照合が `Allowed` になった時点でクライアントがそれを識別として `PlaytestSessionIdentityProvider` に設定する。照合を通らなければゲームを開始できないので、開始後は必ず値がある。開発者モード（`build-info.json` 無し、または Steam 未起動）は従来どおり null のまま。
  出所: ユーザー裁定 2026-09-20 原文「1,2,3全部やって」（2 = SteamID の修正を初回配布の前に入れる）→ 質問「報告・進行記録に入れる SteamID の出どころはどれにしますか？」→ 選択「受け口の検証済みID」
  棄却案: ローカルの Steam API（`SteamUser.GetSteamID()` を起動時に直接読む）／受け口側で上書き（token の steamId で manifest を書き換え、クライアントは直さない）

- **検証機 smoke に SteamID 一致の検査は足さない。** 退行は単体テストで押さえる。
  出所: ユーザー裁定 2026-09-20 質問「SteamID がまた空に戻る退行を防ぐため、検証機 smoke の2段目に検査を足しますか？」→ 選択「足さない」
  棄却案: 足す（manifest と進行記録ヘッダの steamId を smoke が Steam から読んだ ID と照合する）

- **参加同意と前回異常終了の確認は、タイトル画面で出す（odw8 を初回配布の前に実施する）。** Play locally 後のワールド初期化から開始ゲートを外し、WebUI 側のゲート一式（C# の `PlaytestGateBinder`・`PlaytestConsentGate`・`CrashReportGate` 等、React の `features/playtestGate`、bridge 契約）を撤去する。タイトルでは WebUI の CEF を通らず、カーソルもロックされないため、クリックが効かない問題も同時に消える。
  出所: ユーザー裁定 2026-09-20 原文「同意画面もやる」→ 質問「同意画面はどこまでやりますか？」→ 選択「タイトルへ移すまでやる」（2026-09-18 の odw8 裁定を初回配布前に実施）
  棄却案: ロック解除だけ先に（ゲート待機中はカーソルをロックしない。odw8 は後回し）／両方（ロック解除を入れてから odw8 も実施）

- **タイトルのゲートは、今のタイトルに合わせて uGUI で作る。** 起動時照合の表示（`PlaytestLaunchGateView`）と同じやり方でポップアップを足す。uGUI の撤去時は、タイトル全体と一緒に zohw で移す。
  出所: ユーザー裁定 2026-09-20 質問「タイトルの同意画面と前回異常終了の確認を、何で作りますか？」→ 選択「今のタイトルに合わせ uGUI」
  棄却案: タイトルで WebUI（WebUiHost と CEF）を起動し、今の WebUI の同意画面をそのまま使う

- **同意画面の文言と表示規則（保存先ごとに初回1回）は変えず、表示場所だけを移す。**
  出所: ユーザー裁定 2026-09-20 質問「同意画面の中身（文言・いつまた出すか）はどうしますか？」→ 選択「そのまま移す」
  棄却案: SteamID の紐付けを文言に足し、既読の人にも再表示する

- **前回から持ち越した未送信の記録は、初回の「了解」が済むまで送らない。** 既読なら照合通過の直後に送る（現状の「照合通過で即送信」を、同意確認の後ろへずらす）。
  出所: ユーザー裁定 2026-09-20 質問「同意をタイトルへ移したあと、前回から持ち越した未送信の記録はいつ送り始めますか？」→ 選択「同意が済むまで待たせる」
  棄却案: 照合直後に送る（現状どおり。キー配布時の包括同意を前提にする）

### agent前提（実装方針。ユーザー裁定ではない）

- 識別の受け渡しは `PlaytestSessionResponse` に `steamId` を足して読み、`PlaytestLaunchGate` が `Allowed` を置く直前に `PlaytestSessionIdentityProvider.SetCurrent` を呼ぶ。応答の `steamId` が空なら契約違反（`MalformedResponse`）として扱い、`Allowed` にしない。出所: agent前提（`PlaytestSessionResponse.Parse` が既に `token`・`expiresAt` の欠落を契約違反にしている前例）
- タイトルでの順序は「起動時照合 → 同意（初回のみ）→ 前回異常終了の確認（異常終了があった場合のみ）→ Play locally を受け付ける」。前回セッションの退避（`PreviousSessionStartupTasks.RunAtStartup` のうち退避と印の消費）はタイトル表示時に前倒しする。出所: agent前提（ADR 0060 裁定5「前回セッションの印を読む処理は起動時の1箇所」を維持したまま、位置だけタイトルへ移す）
- 異常終了の箱を書いたら、タイトルのアップロード走行（`IPlaytestUploadRequester.RequestUpload`）をもう一度要求して送る。出所: agent前提（照合通過後の走行と同じ窓口）
- 開発者モードのタイトルでもゲートは出す（現状の Play locally 後のゲートと同じく、無人起動 `PlaytestStartGateBypass` では迂回する）。記録を集めるかの判定から「WebUiHost が起動しているか」の条件を外す（同意を出す場所が WebUI でなくなるため）。出所: agent前提（現状の挙動の維持）
- 出展モードの言語選択ゲート（`EventModeStartGate`）は Play locally 後に残り、カーソル中央ロックの影響を同じく受けている可能性がある。本 ADR の範囲外として別 issue に積む。出所: agent前提（ユーザーは「ロック解除」案を採らなかった）
