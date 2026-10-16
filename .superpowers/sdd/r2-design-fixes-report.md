# r2設計既定・局所修正の報告

## 採用した既定と根拠

controller指定の既定を、plan共通制約の無人実行指示に基づいて採用。新しいユーザー裁定とは扱わない。

- D01: 追加設計待ちを維持。100MiB単体/256MiB箱/件数上限を超える必須資料の完全運搬は未達。multipart転送は実装していない。
- D02=A: 細目決定とADR0081の各7点に、提示原文未保全・引用単独の含意検査不能を個別明示。現在の根拠を `.decisions/2026-10-16-開発者報告の実装planを共通制約として無人実行する.md` へ区別した。planとSDD共通制約の出所行も同期。
- D03=B: 4配送状態を維持。既存Countsを在庫既知の派生として再利用し、基底PlaytestOutboxInventoryRead/Unavailableを追加。3非完了状態はInventoryを一つだけ保持、wireはknown{pendingCount,failedCount}/unavailable{reason}、allSentはkindだけ。読取不能を0へ変換しない。ReadDeliveryInventory初回・完了・LocalOnlyの3口、走行中列挙と旧箱補完のディスク境界を追従。未作成だけをDirectoryNotFoundExceptionとして空へ畳み、権限拒否は不明にする。
- D04=A: LocalOnly理由をDistributionUnavailableへ改名。build-infoとSteam両条件の確認を4言語で案内。認証ゲートのbool条件は維持。
- D05=A: HTTP解釈は既存PlaytestSessionへ集約。IPlaytestSessionCredential.AcceptsSubjectをSteam（正整数ID）/Developer（既存dev-判定）双方に追加。成功は検証済みResponseだけ、失敗はOutcome/Detailだけを持つsealed派生。Editor接続確認は同じAuthenticateAsyncを使い成功Response.SteamIdを表示。受け口JSON不変。fixtureはDeveloper応答をdev-testへ追従し、型違いのsubjectが実認証で拒否されるテストを追加。
- W03: 診断writerは*.json.tmpに全量を書いた後、同ディレクトリの最終*.jsonへFile.Moveで公開。コピーは一時JSONを収集せず、writerの実出力本文だけを保持するテストを追加。
- W04: Windowsは既存ACLを継ぎ足さず、現在SIDだけの保護DACLを再構築しSet-Acl後に照合。directory/temp/既存保存先/置換後の最終保存先を制限。既存の他主体明示ACEとFile.ReplaceのACL保持を考慮。Macは既存chmod。
- W01: 完了状態の存在ガードとfailed-delivery例外を確認。補完前の失敗印解除だけでは資料が復旧しないこと、未宣言だけ元SHAで復旧し既宣言本文を変えない手順をREADMEへ補足。自動で恒久保留を解除しない。
- W07: mtime順はplan指定のため変更していない。

## 実測・検証

R=/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/moores-code-review/runs/2026-10-16-1411-dev-bugreport-via-receiver-r2-d7e19fec

- 開始HEAD: 784204537c。Editor PID2989はroot所有、起動/終了操作なし。
- 変更前 `uloop compile --project-path <当worktree>/moorestech_client`: Success=true, ErrorCount=0, WarningCount=0。
- w7-s0 snapshot: 23941d4b2cd06f03050ea7d16dd484a986937f52。applied_diff_checks record: status=ok、71 scenarios。
- D03/D04 compile: Success=true、ErrorCount=0、WarningCount=46（UnitGenerator競合/既存obsolete）。D05 compileはR/refix/w7-contract-compile.json、Success=true、ErrorCount=0、WarningCount=63。
- Web `pnpm test -- ...`: CLI引数が全suiteに渡ったため全139ファイル1069件成功。PauseMenuPanelの既存不完全辞書テストは翻訳欠落warnを出す。配送フォーム4言語は成功。
- Web `pnpm build`: exit0、1134 modules、既存chunk>500kB警告。
- `pnpm exec tsc -p e2e/tsconfig.json --noEmit`: exit0。
- 在庫/runner/scanner/DTO初回23件中21件成功。新規2件はfixture helperが既にディレクトリを作るのに同名fileを書こうとして失敗。テスト準備で空ディレクトリを削除してから閉塞fileを作るよう訂正、再実行2/2成功。実装を既定値で通していない。
- Unity全対象初回: 424件中422件成功。既存BootUploadTriggerTestが非同期化後も直後のcountを期待、PauseMenuWireContractTestが必須Deliveryを未設定。前者をUnityTestで完了待ち、後者をallSentのDTO生成へ追従。
- `python3 -m pytest ...` はpytest未導入で実行不可。既存手順の `uv run --no-project --with pytest python -m pytest -q -p no:cacheprovider scripts/bugreport/tests/review/test_delivery_route_defines.py`: 3 passed（Editor/Development/配布define、実ソースcompile+実行）。
- Windows ACL: R/refix/acl-probe.pyが本番C#のPowerShell文字列を抽出し、winbuildの合成tempだけで実行。旧icaclsでEveryone明示ACEが1件残存した。修正方式でdirectory/temp/既存保存先/File.Replace後が全て protected=True, rules=1, others=0。C# Add-TypeのFile.Replace(source,target,null)も実行しsynthetic-new-tokenへの置換成功。実トークン値・Windows repo/pin/dirtyは未接触。結果R/refix/w7-windows-acl.txt。
- Windows probe初回は長いencoded引数が失敗し文字コード例外、stdin方式へ変更。次はPowerShellの$null→空文字変換でFile.Replaceのbackup引数が不正、C# wrapperで実コードと同じnullを渡し成功。これは製品コードの失敗ではない。
- Web lint初回はJSX内inventory.kindの文字列比較を独自lintが可視文字と誤検出した。表示導出をdescribeFailedDeliveryへ移動し再検査。

## 残件・検証範囲

D01の完全運搬未達、Windows実Editor送信と取り込み、最終Mac録画/本番deployはcontrollerの後続。Windows ACL合成検証をWindows製品送信の完了証拠にはしない。受け口・Python処理・bash処理の本体は変更せず、関連最小検証は三形態resolver検査。世界セーブ形式は変更していない。

## 自己レビュー

未知在庫と既知件数を排他にし、保存rootのアクセス不可をDirectory.Exists=falseで成功扱いする経路を除去。成功セッションのレスポンスは資格情報種別で検証され、Editorと実送信の判定差を除去。新規メタはUnity生成のみ。Localizationの_CompileRequester印はCSV更新に伴うUnity生成変更として含める。

## 確定結果

- `uloop run-tests --project-path <当worktree>/moorestech_client --filter-type regex --filter-value 'Client.Tests.PlaytestReceiver|Client.Tests.BugReport|PauseMenu'`: **425/425成功、失敗0、skip0**。R/refix/w7-unity-tests-final.json。
- Web lint再実行: exit0。Web focused再実行（最終表示helperとunknown schema追加後）: **8ファイル57/57成功**。R/refix/w7-web-focused.txt。
- 新規inventoryテストはUpload直下が10件だったため、既存Delivery配下へ配置し10ファイル制約を守る。Unity生成metaを一緒に移動。
- WindowsArgumentsは単独呼出し元RestrictToOwnerのInternalローカル関数へ移し、規約に追従。

- 最後の自己レビューで、rootだけでなく箱自体のアクセス不可もFile.Exists=falseへ畳まれる同型経路を発見。ScanPending/在庫集計の双方を、箱のDirectory.GetFiles成功後のmarker分類へ統一。読取拒否は既存のUnavailable境界へ届く。関連scanner/runner/在庫/Editor保存テストを再実行。
- 最終Web buildとe2e型検査を再実行しexit0。chunk>500kB警告は継続。

- 箱列挙修正後の関連Unity: **22/22成功**（scanner/runner/件数不明/Editor token保存）。R/refix/w7-unity-final-focused.json。
- 最終明示compile: **Success=true, ErrorCount=0, WarningCount=0**。R/refix/w7-verified-compile.json。途中でrun-tests前置compile中に別compileを発行してUNITY_SERVER_BUSYで拒否されたため、完了後に単独再実行した。
- コードコミット: `34100ff62b fix(playtest): distinguish unreadable inventory and unify session validation`。
- 最終snapshot w7-s1: `63b310b789f5a7ae1a310d137b275094553a2678`。R/refix/w7-final.diff、source差分596 changed lines。
- 最初の反映後record/checkを重ねて実行したため、belt-lineの1件がAnother execution is already in progressとなった。これは検証操作の競合であり製品不具合ではない。両プロセス完了後、checkを単独で再実行して上書きした結果だけを最終結果とする。

- 単独再実行の反映差分チェック: after recordはstatus=ok/71本回収。compareは **status=unverified, new=[], existing=41, unverified=11, after_errors=52**。行対応不能11件のため全検査合格とは呼ばない。
- 診断のpath/code/message多重集合を独立比較: before=52、after=52、新規0、消失0。R/refix/w7-diagnostic-text-comparison.json。行対応未確認はそのまま明示する。
- save_load検査は追加した診断本文テストのJObject.Parseだけに反応。世界セーブ形式は不変だが、指定の既存save/loadテストを追加実行する。クライアントが線・チェーンを含むセーブから起動する経路は自動テストなしのunverifiedとして保持。

## 変更ファイルの役割

- `Client.PlaytestReceiver/{Delivery,Upload}`: 在庫判別union、4状態、箱列挙、失敗伝播。
- `Client.PlaytestReceiver/{Credentials,PlaytestSession*,PlaytestTokenAvailability}` と `Editor/Playtest`: 認証成功/失敗の型、subject照合、共通接続確認、保存ACL。
- `Client.Starter/Playtest/PlaytestBootUploadTrigger.cs`: 起動時の外部列挙失敗をログと配送表示へ伝播。
- `Client.WebUiHost/Game/Topics/C2/PlaytestDeliveryDto.cs`・Web bridge schema/form/i18n・fixture: inventory wireとDistributionUnavailable。
- `Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticWriter.cs`: 一時名から原子的公開。
- `Client.Tests`該当配送/認証/診断/契約テスト、三形態resolver probe、`.decisions`/ADR/plan/復旧README: 追従と回帰検査。

D01の完全運搬と後続実機受入は残るため、製品タスク全体は未完了。担当の局所修正・検証は上記の範囲で完了し、Editorを停止せずrootへ引き継ぐ。

- 追加save/load検査の確定結果: 235/235成功、失敗0、skip0。R/refix/w7-save-load-tests.json。
