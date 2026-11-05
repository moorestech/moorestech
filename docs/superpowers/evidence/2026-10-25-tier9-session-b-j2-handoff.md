# Tier9 統合 — セッション B の W2／J2 用引き渡し

## B担当の完了

**Task 25・26 は実装・コンパイル・関連テスト・独立タスクレビュー・push済み。** 最終コードコミットは `c41dfa51e4aab23b6342594c373e60589836d8b1`、ブランチは `origin/feat/tier9-integrated-tests`。

これは **B側条件の完了** であり、J2全体の通過ではない。Aは自分のTask 12〜24完了と最終マスタ・ピンの受入れを確認し、Bブランチを通常マージして自分のEditorでcompileとG4/G5を再実行する。全ブランチレビュー・PRは引き続きA担当。

## J1からの取り込み

- Aの正式J1 PASSと `docs/superpowers/evidence/2026-10-25-tier9-j1-acceptance.md`、ユーザー裁定 `7beff877ff` を確認後に再開。
- `688cc1b1d85f839c6fd5db57f426ac5982d90d28` で `75a43eb0033d01c10734d014f2c5e2eb573443e4` を通常マージ。squashなし、競合なし。
- W1マスタの書込み所有権はAのまま。マスタrepo・本番CurrentVersion・ピン・スキーマをBの成果として変更していない。

## Task 25 — G4

コミット `4b34b5901e`。独立レビューは Spec ✅／quality Approved、Critical/Important 0。

- テストmodにアイテム6、family2、積荷2、レシピ3、研究3、拠点の受入れ衛星2種を追加。既存validatorの積荷表件数4→6も対応。
- 実processor・tick・研究・ロケット初回コマンド・後続自動打ち上げ・ケーブルプール・接続・昇降を連結。
- ICチップLv1→GPU／衛星→計算データ10→研究で消費・製作上限2→Lv2衛星の計算データ20→拠点研究→ケーブル50×2→接続でプール100→0→昇降で計算データ10。
- 上限拒否と拠点投入は実 `InsertItem` を通す。処理成果や到着成果の直接注入、研究上限・プールの直接設定は行わない。
- 指定regex **352/352 PASS**、G4本体PASS。データ破壊3種類を対応assertが検出。
- 追加Server.Tests全体は3320件中3299 PASS／21 FAIL。W1原XMLとの失敗fullname多重集合比較は **欠け0・余り0**。既存IgnoreCI手動Surface検査21件の未達を保持する。全assembly合格とはしない。

## Task 26 — G5

コミット `c41dfa51e4`。独立レビューは Spec ✅／quality Approved、Critical/Important 0。

- fixtureは `origin/master` の `e855a586674d4f57fc1a770e3760439bc37333fd`（版7）から切った一時worktreeの実Editorで生成。実機械を加工途中まで進め、正規の `AssembleSaveJson` で保存したもの。
- SHA256: `94b89f2c42f4450b55c48d524d26534bdbef5218098d72f927b7242b6405a2bf`。
- 6研究＋保持するG4研究2件の完了、3ブロック・8表示の解放は、生成器でmasterの保存型を用いて追加した **合成記録**。旧本番マスタで実際にプレイして作ったセーブとは主張しない。生成器・元SHA・由来をlogsへ保存。
- 計画サンプルで抜けていた表示8件を追加し、両テストの移行前に6研究・3ブロック・8表示すべての存在／完了／解放済みを正にassertする。保持3研究、機械加工中・残時間・pending出力、パイプ5e9、旧版に新版キーが無いこともassert。
- chain出力をpruner前に検査してリセット・補完を確認し、別テストで本番ロード入口・V7原本backup・研究再実行による解放・パイプ量・加工再開からTest3の0→1まで確認。
- G5本体 **2/2 PASS**、指定関連regex **189/189 PASS**、追加SaveLoad回帰 **376/376 PASS**、最終G4再実行 **1/1 PASS**。
- リセットを止めると実ロード側はprunerのため緑のまま、chain側は赤になることを変異検査で実測。表示削除を止めた変異もchain側が検出する。

## コンパイル・運用・範囲

- 両タスク最終compile **ErrorCount 0**、外部依存チェックexit 0。新テストは本番マスタを読まない。
- 永続差分はテストmod・G4/G5・関連validator件数・引き渡し文書のみ。本番セーブ版は統合側8のまま。最終確認時の `origin/master` は **CurrentVersion 7**。J2判定時にAが再fetchして確認する。
- 実Claude `--session-id` のPIDをownerとするreceiptを登録し、reaper保護集合に含まれることを実測。使用したEditorはすべて停止し、不在を確認済み。一時 `g5-fixture-master` のworktree／ブランチも片付け済み。他者Editor停止なし。
- Task26の変異QAでは、tests worktree内の製品移行2ファイルを一時変更して検出を確かめ、復元した。最終差分はテストのみで、復元後にcompile・189件・G4を再実行済み。「製品ファイルに一度も触れていない」とは報告しない。

## Aの最終レビューに渡す所見

1. Task25最終compileは警告19件、Task26は138件。新規ファイル由来は0件だが、BASE同条件比較は無いため全件既存・総数不変とは断定しない。Task26の内訳はCS0436=67、CS0618=34、CS8632=16、CS0105=6、CS0219=5、CS0168=5、CS0659=4、CS0108=1。
2. G5生成時の清浄室state復元見送り、実ロード時の `nonJson=18 unparsable=0`・世界作成日時欠損の既定値ロード診断を保持。無警告・無雑音とはしない。fixtureを手修正して隠していない。
3. Task26の補助 `check_all` のdead_member結果はstale。`confirmed 0`を同検査の合格としない。Aの全ブランチレビューでは現DLLによる結果を確認する。
4. Task25途中の `ReceivedPacketLogWiringTest` に一度 `IOException: Directory not empty`。単独3回・最終全体ではPASSだが、後片付けフレークという原因説明は推測で、無関係を実証した扱いにしない。
5. `~/.local/bin/uloop` が生バイナリのためowner receiptが自動登録されない状態を観測。Bは手動登録＋保護検査で対応し、共有配置／reaperは変更していない。
6. W1からのG2旧master65件条件と名前ペアAPIの契約差、checkout非推奨警告はW1引き渡しを参照。原手動21FAILと最終ピンG1/G3/CI必須条件はA33/A34どおり維持。

## 証拠とAの再実行

原JSON/XML、owner receipt・停止、変異、生成器の証拠:
- `moorestech_logs/harness/tier9-integration/session-b/task-25/`
- `moorestech_logs/harness/tier9-integration/session-b/task-26/`
- `moorestech_logs/harness/tier9-integration/g5-fixture/`
- SDD報告・独立レビュー: `moorestech_logs/harness/sdd/feat-tier9-integrated-tests/`

A側で通常マージ後、compileに続けて少なくとも次を実行する（本体統合worktreeのclient projectを指定）:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tier9ComputeLoopEndToEndTest|Tier9GoldenSaveMigrationTest"
```

関連回帰の再現filterはplan Task25 Step6／Task26 Step7を参照。マシン共通XMLのPID・start-time・filter整合を確認し即コピーする。Bの親タスクは `moorestech-xyuaf.2.3`。B担当はここで終了し、Aの統合ブランチにはBからマージしない。
