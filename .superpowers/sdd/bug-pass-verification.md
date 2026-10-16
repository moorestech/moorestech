# 最終bug-pass反映後の検証

## 対象と根拠

- 対象HEAD: 3d1818b45e。8件修正7625504170、Refix2件3d1818b45e。
- R: /Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/moores-code-review/runs/2026-10-16-1411-dev-bugreport-via-receiver-r2-d7e19fec
- 最初に読んだ正本: R/bug-pass/codex-runner/result.json、R/bug-pass/integrated.md。26発火0失敗、20/20回収、Refix round2 Critical0。
- Editor PID36131はroot所有。Ready通知後に検証を開始し、起動・終了は行わない。
- 修正前周のD01未実装という報告は当時HEADの状態。今回B03で分割経路が実装されたため、現行ADR/plan/READMEは実装済みの構造と実測未確認範囲へ更新する。新しいユーザー裁定とは扱わない。

## 構造

CommitEvidenceTransfer.Prepareは、未宣言箱の単体MaxFileBytes・総量MaxBundleBytes・MaxBundleFilesの超過で本体/master bundleを分割する。各断片はmin(MaxFileBytes, MaxBundleBytes)以下、子転送の宣言はchunk1件。番号と断片の長さ/SHA256をrepo/commit-transfer.jsonへ保存し、再送は同じ計画を使う。親の宣言は元bundleと__attachmentsを除外し、転送計画をRequiredとして送る。

PlaytestUploadAttachmentsが子転送を先に完了させる。Workerは子単独のpending索引を作らず、親completeで全断片の存在と長さを照合して親だけを公開する。Workerは断片SHA256の形を検証するが、内容ハッシュの照合は取り込み側restore-commit-transfer.pyが担う。ingestは復元成功後に公開/ACKし、prepare-run/prepare-masterも同じ復元口を使う。復元は断片長・SHA256・合計長・パスを検証し、完成したbundleだけos.replaceする。

分割対象はコミットbundleであり、他の必須資料だけで上限超過する箱を無条件に運ぶ変更ではない。既宣言本文を分割形式へ変更しない。容量のみの未宣言保留は分割計画完成後に解除する。旧Editor資料未補完だけの保留は元SHAの補完成功後に戻し、他の恒久失敗を解除しない。

B08のbundle生成はorigin/masterの前提を省く自己完結形式。本体/masterとも空repoへのfetch後、元SHAと一致する実gitテストがある。B07はbox.lockを補完と配送で共有し、bundle出力は実行ごとの一時ファイルを完成後に公開する。

## カバレッジの区別

|対象|検査の実体|限界|
|---|---|---|
|単体100MiB超|NUnitで104857601byteのsparse元ファイルを作り、実ファイル分割・長さ/hash計算。偽HTTPへ2子を送り親成功を検査|本番HTTP/R2ではない。実git bundleの内容ではない|
|箱256MiB超|NUnitで90MiB×本体/master/large.bin=270MiB、各ファイル100MiB以内を作り実分割|同上|
|件数128超|129個の小実ファイル（本体/masterを含む）を2子＋128件親へ分け、各元bundleと断片のbyte一致・Skippedなし・3completeを検査。Transfer4/4成功|本番HTTP/R2ではない|
|本体/master|総量ケースで両bundle、別実gitテストで両方空repoへ復元|大容量実git履歴の本番送信は未確認|
|再送|偽APIのprepareで一過性失敗→次走行成功、転送計画文字列不変を検査|実ネットワーク切断/Editor停止の大容量再送は未確認|
|Worker|400MiBの4断片計画をparserへ入力。3byte断片で欠損409→子完了→親READY、ACK後子再送の冪等性|400MiB実データをR2へPUTしていない|
|受信再構築|Pythonで本体/master各11byteの2断片を2回復元。欠落/hash/長さ/path/合計破損で元bundle維持|C#出力から本番ingestまで連結した大容量検証は未確認|
|プロセス間競合|同じ箱の2ロック取得拒否と解放後の取得をNUnitで検査。別実gitテストで他writerの最終.lockがある場合も正常bundleを保持|2Editor同時実機試験は未確認|

## 他の実行結果の出典

Worker型検査・180テスト、Python33テスト、シェル3スイート成功はR/bug-pass/codex-runner/result.jsonのapply.tests記録に基づく。今回担当が再実行した件数ではない。Web本体は14e7c9ffe1からbug-pass修正で変更されていないため再実行しない。

## 世界セーブと未確認Warning

14e7c9ffe1..3d1818b45eの変更一覧に世界セーブモデル・WorldSaveAllInfo.CurrentVersion・Migrationの変更はない。報告bundleの転送/診断形式変更と世界セーブ形式を区別する。指定SaveLoad系の再実行結果は末尾へ記録する。

BW01: PlaytestBootUploadTriggerのGate取得後worker待ち/main thread復帰に再生終了tokenが伝搬せず、停止後に旧requesterが要求し得るという未再現Warningは保持する。実Editorで誤送信/表示破壊を観測していない。新設計や無根拠な対処は追加しない。

本番Mac送信/取り込みは後続担当へ渡す。Windows実Editor送信はライセンスexit198による未確認を維持する。合成ACL検証の成功をWindows送信の証拠にしない。

## 初回440件の失敗分類と追加修正

`verification-unity.json`: 440件中417成功、23失敗。実送信Explicitも正規表現の明示選択に含まれたため、後続は `^(?!.*DeveloperReportRealReceiverCheck)(?:Client.Tests.PlaytestReceiver|Client.Tests.BugReport|.*PauseMenu)` へ限定する。

- Transfer2件: 新fixtureはprepareの一時失敗を1回だけ仕込み、既存の同一走行内再試行で成功した（Expected0/Actual1）。既存既定回数+1のprepare失敗を仕込み、初走行complete0/親UPLOADEDなし、次走行子2+親1のcomplete3を確認するよう修正。128件超追加と合わせ4/4成功、f001d9dd37。
- 容量旧期待3件: 単体超過2件は親含む4completeを観測し旧「保留」期待と矛盾、総量超過1件もSent1。新仕様で成功すること、親に転送計画とworldが残ることを検査へ変更した。
- 欠損記録不能6件: 容量超過が回復可能になり欠損記録経路に入らずSent1。記録不能の失敗試験を消さず、作成済みbundleの消失を入力としてmanifest/FAILED/ATTEMPTS記録失敗で無HTTPを検査する。
- 既宣言1件: 新PrepareAttachmentsが既宣言超過でIOExceptionを投げ、既存の必須資料検証・UPLOAD_FAILED記録より前にretry待機へ入る。FAILEDが存在しないことを再現（line69）。分割せず既存検証へ戻し、元manifest不変・HTTP0・転送計画なし・FAILEDありを検査する局所修正。
- Runner8件とInventory1件: 既宣言テストに続いて偽API呼出0・状態不一致。実送信を除いた隔離12件でも既宣言1+同じ9件が失敗、2成功（verification-isolated-before.json）。既宣言の非同期retryが共有Gateを保持したまま終了する波及と整合する。実送信だけが原因とは断定しない。修正後同じ経路を含め再実行する。
- 未補完Editor1件: 正常箱がUPLOADEDにならなかった。上記非同期Gate残留の後続症状候補であり、再実行で確認する。
- 実送信1件: DeveloperReportRealReceiverCheckが180秒timeout。Editor.logでID `20261016_073514_b3d96725`、実outbox `/Users/sakastudio/Library/Application Support/moorestech/BugReports/outbox/20261016_073514_b3d96725`、prepare HTTP405 method-not-allowedを観測。実outboxは変更/削除せず後続担当へ引継いだ。これは意図した実機検証ではなく対象フィルターの混入であり、送信成功の証拠に数えない。

初回XML: moorestech_client/.uloop/outputs/TestResults/20261016_073838_5822230_a6852f3d12a24770adc17cc7e123aa7a.xml。隔離XML: 20261016_074105_7952640_d23d56e634a94966847b6123d6429108.xml。実行JSONはR/bug-passへ保存。

## 実行コマンドと出力

すべて専用worktreeから前景で直列実行。`uloop compile --project-path ./moorestech_client` は初回Error0/Warning0（verification-compile.json）、Transfer編集後Error0/Warning60（verification-count-compile.json）、既宣言修正後Error0/Warning42（verification-hold-compile.json）。後2回の警告はUnitGenerator重複型・既存obsolete API等で、今回変更したファイルを指す警告はない。完全な警告本文もJSONへ保存。

`uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value CommitEvidenceTransferTest` は4/4成功（verification-transfer.json）。

`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'PlaytestCommitEvidenceTest.既宣言|PlaytestUploadRunnerTest|PlaytestInventoryReadFailureTest'` は修正前12件中2成功10失敗（verification-isolated-before.json）。

`git diff 14e7c9ffe1 --name-only -- 'moorestech_server/Assets/Scripts/Game.SaveLoad' '*WorldSaveAllInfo*' 'moorestech_web/webui'` は出力なし。世界セーブ/移行/Webは変更しない。

`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value '^(?!.*DeveloperReportRealReceiverCheck)(?:Client.Tests.PlaytestReceiver|Client.Tests.BugReport|.*PauseMenu)'` はTestCount441 / Passed440 / Failed0 / Skipped1（実送信Explicit）、2026-10-16T07:46:04Z完了（verification-unity-final.json）。新規件数境界と大容量2ケース、既宣言、Runner8件、Inventory、未補完Editor箱も通過した。初回23失敗のうち実送信混入1件を除く22件を解消し、実送信は未検証として残す。既宣言修正とテスト整合は466c8fdfb4。

変更コミット: 7f89b2a972（現行文書/meta）、f001d9dd37（128件超・再送回帰）、466c8fdfb4（既宣言検証回帰）。コードはCommitEvidenceTransfer125行、Transferテスト132行、既存Evidenceテスト199行。追加C#ファイルなし、metaはEditor自動生成分のみ。

`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.(UnitTest|CombinedTest)\..*(SaveLoad|SaveJsonFile)'` は235/235成功・失敗0・skip0、2026-10-16T07:46:40Z完了（verification-save-load.json）。世界セーブ形式の変更はなく、既存互換テストも維持された。

残件は本番Macで自己完結大容量bundleの子転送→親公開→ingest復元/ACKを通すこと、Windows実機送信未確認、BW01未再現Warning。これらを単体/合成成功で完了扱いにしない。Editor PID36131は起動/終了せずrootへ返す。実outboxの混入IDは削除しない。

## 機械規約の最終補正

9730a5f34f: PlaytestUploadRunnerのUploadPendingAsync→ScanPendingの外部ディスク列挙、PlaytestBootUploadTriggerのComplete→ScanForEvidenceの外部ディスク列挙を確認し、各try直前へ短い日英の境界根拠コメントを置いた。catchの対象・通知・制御フローは変更しない。SteamTicketSessionCredentialの`id > 0`を同値の`0 < id`へ変更した。

`uloop compile --project-path ./moorestech_client` はSuccess=true、Error0、Warning8（verification-mechanical-compile.json）。意味不変のコメント/比較式補正なので大規模テストは再実行せず、直前の関連440/SaveLoad235成功を維持する。

`python3 .agents/skills/moores-code-review/scripts/review_diff.py 090a8f57dddc62bd2b4091eb5e772d41db1a1e1c..HEAD` で全PR差分をpatch-mechanical-final.diffへ出力し、`python3 .agents/skills/moores-code-review/scripts/deterministic_checks.py <patch> --repo-root <専用worktree>` を実行。checks-mechanical-final.jsonはconfirmed0・comparison_operator0。他の候補を新たに裁定済みとする主張ではない。全実装ファイル200行未満を維持。
