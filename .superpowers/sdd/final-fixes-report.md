# 最終レビュー修正報告

## 状態

担当範囲の実装修正と最終検証を完了。大容量bundleの完全運搬（D05の新protocol）、Windows実送信、Mac録画、本番deployは未完了であり、この担当の完了に混ぜない。Editor PID 37940はcontroller所有のまま停止していない。

## 適用項目と既定

| 項目 | 実装・判断・影響 |
|---|---|
| D01 / C01 | 案A。開発者JWTに現credentialHashを署名し、Steam/Developerの主体を判別unionにした。交換とuploadは同じ有効hash・双方向索引を検査。発行応答はissueSessionResponse、認可はauthorizeUploadへ集約。同秒の失効・同名再発行でも旧JWTのprepare/completeは401、新JWTは200。新しい永続世代は追加しない。 |
| D02 / C02 | 案A。ReadDeliveryInventoryとPlaytestOutboxDeliveryCountsでpending/failedを分ける。LocalOnly/Pending/Refused、DTO、Zod、4言語UIまでFailedCountを保持し、両方0の場合だけAllSent。UPLOAD_FAILEDの自動再試行除外は維持。 |
| D03 / C25 | 案A。.decisionsとADRの境界を「取り込みからの自動投入は行わず、人のenqueue後のinbox監視/起動は継続」に整合。新しいユーザー発言・userrulingは作成していない。 |
| D04 / C09 | 案A。診断Directoryに再生ごと一意のIDを保持し、writerとcollectorの共通Resolveがサブdirを返す。IDはSubsystemRegistrationで更新、既存証拠は削除しない。READMEに寿命と手動整理方法を記載。 |
| D05 / C10 | 追加設計待ち。既存PlaytestUploadPathの上限制御（PlaytestReceiverConfig.MaxFileBytes=100MiB、MaxBundleBytes=256MiB）でcommit bundleが除外されると、即UPLOAD_FAILEDで保留し画面へFailedCountを渡す。未宣言ならmanifest.missingとbundle-statusをfailed-deliveryへ更新。既宣言の本文・宣言は不変で、理由は失敗印とログに保持。新しいmultipartや複数転送単位は実装していない。大容量資料は依然運べないので完全解決と数えない。 |
| D06 / C12 | 案A。MainGameのEditor起動で、送信前に旧箱を補完。Game側CompleteExistingが元manifestの本体/master SHAを明示repoで確認し、既存Writeを使う。元HEAD/branch/dirtyや他フィールドを現在値に置換しない。既宣言・不明SHA/repoは理由付きUPLOAD_FAILEDと未宣言時のMissingで保留。配布箱は対象外。 |
| D07 / C05 | 案B。Task9の最大200集約件を維持。実ログの時刻/tickを比較キーから外し、種別/本文/スタックで連続反復を集約。エラー優先、同順位は末尾優先、表示は元位置順。元ログ行範囲・反復末尾時刻・除外件数を記録。上限外の原因を落とす可能性は残るため原ログを保持。 |
| D08 / W09 | 案A。revokeのrevoked/notFound/supersededを判別結果にし、管理routeは200/404/409。遅延CASが新発行を破壊せず409 revocation-supersededで返る回帰テスト。 |
| R1-C01 | contact_sheetのunlink/一時領域/replaceの外部IO失敗を動画節に隔離しstderrにも記録。旧画像が残った場合も今回成果物ではないと表示し、triage.mdと独立節を保存。macOS immutable旧画像を使う実CLIテストを実行。 |
| R1-C02 | 動画あり実CLIを制御されたffmpeg実行ファイルへ通し、呼出し/新規画像/成功文を検証。tick100/200のRailTypeGuid先頭8桁を変え、最新列を比較。実装コピーへの動画呼出削除・古い型選択の2変異がいずれもexit1になることを実測。 |
| R1-C03 | 同じ既存保存先へchmod500の親で障害注入。直接上書きは成功するが一時ファイル作成は失敗する条件を実測し、SaveFailed・同一pathのReadで旧値・temp残骸無しを確認。直接上書きへ一時変異した実EditorテストはExpected SaveFailed / But Savedで1件失敗。原実装をfinallyで復元。Windowsでは同等の障害注入は未実施と明示。 |
| W03 | 独立master作業repoに固有commitを作り、master bundleのimport SHA・temporary ref解放・Missing無しを検証。 |
| W04 | FakeUploadApiにdeveloperToken入力列とprepare bearer列を保持。first→replacementの交換とtok→token-replacementの実利用順を検証。 |
| W01 | 実権限測定から局所修正。下記参照。Windows ACLの実機検証は未実施。 |

## W01の権限実測

秘密値は読まずstatだけで確認した。

```
drwxr-xr-x 755 sakastudio:staff .../moorestech
drwxr-xr-x 755 sakastudio:staff .../moorestech/PlaytestReceiver
-rw------- 600 sakastudio:staff .../PlaytestReceiver/developer-token.txt
-rw-r--r-- 644 sakastudio:staff .superpowers/sdd/token-permission-probe.tmp
```

最後のtempは同じEditor内でFile.WriteAllTextした非秘密の作業tree内ファイル。stat後に削除済み。現tokenが600でも、新規tempは644となるため次回の置換で権限が緩む懸念を確認した。

RemoteExecDirectoryのOSコマンド前例に沿うEditor保存口専用helperを追加し、秘密値を書く前に親dirの他ユーザー権限を除去、公開前にtempも所有者限定にする。Unixは`chmod go-rwx`で所有者の既存権限を増やさず、Windowsは既存前例と同じicaclsの継承解除/所有者grant。失敗は保存結果SaveFailedへ戻り既存Windowの画面とErrorログに届く。既存の正常tokenは公開しない限り保持。一時物の後片付けも失敗詳細に残す。実機の既存資格情報/親dirはこの検査では変更していない。

## 検証コマンドと実出力

ログはこの報告と同じ `.superpowers/sdd/final-fixes-*` および `R/verification/w7-final-fixes/` に保存。最終確定値は後段追記。

- 初回 `uloop compile --project-path <worktree>/moorestech_client`: socket i/o timeout。Editor.logの当該PIDでimport完了・C# error無しを照合し再実行 → Success=true/ErrorCount=0。Editor再起動・停止なし。
- `pnpm test`（tools/playtest-receiver）: `Test Files 19 passed (19) / Tests 176 passed (176)`。
- `pnpm typecheck`（同）: exit0。
- `uv run --no-project --with pytest python -m pytest -q -p no:cacheprovider scripts/bugreport/tests/test_triage.py scripts/bugreport/tests/review`: `28 passed in 1.92s`。
- `pnpm test`（moorestech_web/webui）: `Test Files 139 passed (139) / Tests 1065 passed (1065)`。
- `pnpm build`（同）: exit0、built in 5.03s。500kB chunk warningあり、ビルド失敗ではない。
- `bash scripts/playtest/tests/reports/test-find-reports.sh`: `PASS find-reports pagination, mtime ordering, limit and receiver failure`。
- `bash scripts/bugreport/tests/test-prepare-run.sh`: `OK`。
- `bash scripts/playtest/tests/test-dev-token.sh`: `OK`。
- `uloop run-tests ... --filter-type regex --filter-value 'Client.Tests.PlaytestReceiver|Client.Tests.BugReport|PauseMenuTopicDeliveryTest'`: `TestCount=382, PassedCount=382, FailedCount=0, SkippedCount=0`（W01局所修正前）。
- `uloop compile`（W01前の最終）: `Success=true, ErrorCount=0, WarningCount=62`。既存UnitGenerator/obsolete警告が主。
- 保存の直接上書き変異: 実Editorにて `TestCount=3, PassedCount=2, FailedCount=1, SkippedCount=0`。失敗は `同じ保存先の書込み失敗でも旧トークンを維持する` の `Expected SaveFailed / But Saved`。原実装復元後compileはSuccess=true/ErrorCount=0。
- Python変異: CLI動画変換を削除 → ffmpeg呼出markerが無くtest失敗。RailTypeだけ最古snapshotへ変更 → 最新型00000200の列比較が失敗。ともにexit1。元treeは変更せず一時コピーで実施、コピーは削除済み。
- `git diff --check`: exit0。変更コードは全て200行未満。変更対象ディレクトリはコード10ファイル以下。

## 修正中に検出して直したfixture不備

- NUnitのPlatform指定をUnityのOS名で書いたためMacの保存障害試験がskipになった。OS判定を明示し、Macで実際に実行して成功を確認した。
- 大容量fixtureがrepoディレクトリを用意していなかったため4件のDirectoryNotFound。fixtureを修正し再実行。
- `commits.bundle: created`の否定が`master-commits.bundle`にも一致したため1件失敗。行単位の否定へ修正。製品の状態ファイルは既に正しかった。

## 残件・制約

1. D05の大容量bundle完全運搬は追加設計待ち。今回の配送不能保留は完全運搬の代替完了ではない。
2. W07 Windows実送信はUnityライセンスexit198の引継ぎ未完了。Windowsの保存障害/ACL実機検証も未完了。ライセンスや課金設定は触っていない。
3. Mac録画・本番受け口deployはcontrollerの専任後続工程。この担当では行っていない。
4. W05/06の引用原文不足は未解消。ユーザー発言や対応する推奨7点の原文を創作していない。
5. W02の未走査Pending(0)とW08の将来route追加時の型検査懸念は既存のまま。新しい状態/基盤を独断で追加していない。
6. 配布build smokeと4言語の実画面録画は未実施。4言語はproduction CSVを用いたフォーム描画unitテストで確認。

## コミット

- `c3dd6010c0 fix(review): bind developer sessions to issuance and preserve triage evidence`
- `0513a9d7e0 fix(review): preserve failed delivery evidence and isolate developer credentials`

## 最終確定検証と反映差分

- W01局所修正後compile: `Success=true, ErrorCount=0, WarningCount=46`。
- W01局所修正後のUnity対象: `TestCount=383, PassedCount=383, FailedCount=0, SkippedCount=0`。Macの同一保存先IO失敗、親700/新規600/再保存600も実行して成功。
- `w7-s0`: `d26ab904163dff93c9b7c18d595b079086a7f226`。変更前scenario診断71本を記録。
- `w7-s1`: `a266467047ac765fdb3363206c43285e099db510`。反映差分 `R/refix/w7-final.diff`、983 changed lines。Rは依頼に指定されたreview run。
- `applied_diff_checks.py check ... --from w7-s0 --to w7-s1 R/refix/w7-final.diff`: exit0。ただしscenario判定は `status=unverified, new=[], existing=41, unverified=11`。行番号を対応できない11件が残るため「全検査合格」とは扱わない。
- 診断文面を追加比較: before/afterとも71 scenario・52診断、path/code/messageの多重集合で新規0/消失0。`R/refix/w7-diagnostic-text-comparison.json` に保存。行対応の未確認自体は残る。
- `R/refix/w7-applied-diff-checks.json` は保存済みbefore/afterへ同じcompare/save_load関数を適用してチェック結果を再保存したもの。runnerや検査ロジックは変更していない。
- 追加のSaveLoad系: `uloop run-tests ... --filter-type regex --filter-value 'Tests\.(UnitTest|CombinedTest)\..*(SaveLoad|SaveJsonFile)'` → `TestCount=235, PassedCount=235, FailedCount=0, SkippedCount=0`。
- save_load検査は報告manifest/補完コードのキーワードへ反応した。世界セーブの形式変更ではなく、CurrentVersionやmigrationは変更していない。チェックが示すclient線/チェーン含むsaveからの起動経路は自動テスト無しのunverifiedとして残る。

担当セッションは閉じてよい。controllerはD05追加設計待ちと上記未検証を保持し、最終bug-pass・Mac録画・本番deployへ引き継ぐ。
