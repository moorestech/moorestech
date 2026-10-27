# Tier9 Task24: 最終pin検証・全体レビュー後の検証コード修正

## 現在地

Task23の容量C1は独立再レビューSpec適合/Approvedで解消し、C2は明示裁定で閉鎖した。`moorestech-xyuaf.2.5` はclose済み。ただしTask24のmaster全体レビューで時間モデルの別の欠落が再現されたため、時間バランスの受入れは再び保留する。マスタPRは未公開、Task24/J2は未完了である。

- BODY HEAD: `10fedacae0a222be69b88f05e088f7278ed7b4d4`（本チェックポイント直前）。pin=`98e0104f013bf2842ffe35f467c5a9009c985985`。
- MASTER HEAD: `db92f0afdf271663316204b334ae81eefbeae553`、push済み/clean。全体レビューのApply14件、RW1、テスト配置を3コミットで確定した。BODY pinはまだ新HEADへ更新していない。
- 新規追跡: `moorestech-xyuaf.2.6`。共有clone incident `.2.4` は別件として未解決。

## Task24 phaseAの実測

BODY `33f4f08214..10fedacae0` の3コミットでpinを更新し、出荷blocksからregistry fixtureを忠実に再生成した。予想外の差分はTier9前からのstale fixtureであり、独立レビューが既存SectionStackViewとDTO経路を確認した4種をintentionalGenericへ列挙した。対象Playwrightは1 passed。

専用QA Editorでpin98e0104由来rootのG1/G3を実行した。G1必須キー破損2種、G3順序破壊を負例として失敗し、復旧は全55研究成功。batch終了コードはG1正0/負1、G3正0/負1。上位チップ実投入はLv1のみ/Lv5のみ/上位4種/5種混在で全55研究成功、最大枠36/36/39/40。通常検証区間の内部Errorは0。過去の明示MASTER入力だけの成功を流用していない。

手動Surface21件はLegacy1＋1tile5件のみPASS、3tile10＋Heavy5件は未確定。DirectoryNotFoundを観測したが削除者は未特定。fixtureの共有cacheへ別サーバーのCollectorが干渉する静的経路と、共通XMLの上書きがある。同じ条件でretryしない。Factoryの既存WorldDataDirectoryを使う局所隔離案は整理済み、まだ実装していない。

## master公開前の全体レビュー

moores-code-reviewを指定のCodex runnerで実行した。初回solはmodel capacityで失敗したため、run内wrapperでgpt-6-astraを明示し、役割別reasoning強度と全系統の発火を維持した。

- runner: 発火36、失敗1（再試行で回収）。計画28=reviewer6＋Fable相当1＋分割調査21、全28応答、欠員0。外部Codex3本も回収。Fable名の役割はCodex-hostの正式モデル写像で実行したもので、独立のFable APIを使ったとの主張ではない。
- user-intent §5形式ゲートPASS。内容上の参照不能を形式合格で消していない。
- 統合: Critical21＝自動修正14＋設計選択5＋保存境界の追加検証待ち2、Warning5、Info18、suppressed0、純スタイル棄却1。
- Apply/Refix2周の最終反映差分はCritical0。ただし全ブランチ受入れではない。
- MASTER `7db4ac3` / `cbf2f63` / `db92f0a` を独立opusが点検・確定。数量比較RW1は既存EPS=1e-9へ統一しred/green、Python各200行未満。
- 現HEAD: tier9 tests86、progression54、nodegraph10、research-sync4、apply6成功。research-syncのResourceWarning4件は残る。G2許可19でPASS、sync/ng PASS、**G6は329検査中3 FAIL**。
- Step7設計裁定・裁定反映後Refix・Step7.5 bug-pass・最終レビュー記録は未了。目的だけを渡す独立評価2体も未実施であり、PRを公開していない。

## 再現した時間モデルの欠落

1. D01: Bの3ライン実台数では品質63・生産性117個を要するのに、製作費は18/45個分だけ。204分の品質適用時に未払い90チップ/在庫34.6778、348分の生産性適用時に未払い450/在庫0のまま効果が付く。6.45hはこの欠落を含む計算値である。
2. C08: 発射台135秒ごとにGPU1個＝同品質チップ20個を要するため需要0.148148/s。現G6はS2でも品質Lv1×3のS1構成へ固定され、Lv2供給0.117131/s、Lv3/4は0として3件失敗する。これは当該構成の不足であり、既存仕様で4.5hが不可能という証明ではない。
3. 別役が既存スタック上限3/6/10、研究段階ごとの製作Tier、実3枠、支払済み装備・在庫繰越を使う条件付き収支を検討した。既存室内13台/ラインの範囲で成膜の品質だけを上位化し、速度モジュールを適用すれば、前段からの在庫で後段の需要を満たす算術例がある。
4. ただし開始装備はチップ3570＋他材料、工作装置6555秒を要する。そのS1内の支払・製造待ち、上流機械・電力・搬送、GPU→衛星の仕掛時間が未接続なので、この条件付き例をG6合格にはしない。

## 検証コード修正の方針（質問撤回・controller既定）

ユーザーの「なにが意思決定の軸なの？」という指摘を受け、以下を人間の選択として上げた質問は撤回した。ユーザーがAを選んだという承認ではない。既存仕様・費用・目標を維持する検証不具合の修正はcontrollerがAを既定として判断する。D05も既存runtimeへ整合させる。実際のゲーム内数値/目標の変更が必要と実測された場合のみ相談する。以下の別案は技術的な比較記録として残し、承認待ちに使わない。

### ① D01＋C08: 実製作と段階別生産をどうモデル化するか

**A（推奨）:** S1は現構成を保ち全実台数分を支払ってから効果を適用する。S2は研究済み上限（現在3/6/10）と製作Tier、実装着・製作待ち・在庫繰越を同じ状態へ接続する。研究費150/300/600/1200、4.5h目標、レシピ/品質値、固定工場定数は変えない。できあがる形は研究完了と装着完了を分けた計算本体、既存run返却への射影、S2/S3への支払済み状態の接続。ユーザー裁定は覆さず、誤モデルの6.45h等は再計算する。

**B:** 製作18/45を維持し、装着機と未装着機が混在するラインを個別にモデル化する。実機別配置レコードが必要で、現在の全実機一律効果を置き換える。未払い効果はどちらでも認めない。正確なモデルでも不足が残る場合の数値変更は、不足量を出してから別裁定とする。

### ② D02〜D04: 既存仕様を守る最小の検査修正を一括採用するか

**A（推奨、一括）:** 爆弾は現仕様の確定1出力を検査、祖先集合は配列順に依らず完全探索、実行不能レシピは最終状態で共通判定する。新型0、現master仕様・研究費・目標は不変。dead_recipesへ状態引数1つと共有判定1本を導入し、実行条件を重複させない。

**B（個別指定）:** 爆弾は任意出力バッチ数まで算出、循環グラフは集合を返さず明示拒否、レシピは到達計算中の実行可能GUID集合をReachStateへ保持する。元のdesign.mdの代案を維持するが、現仕様の欠陥だけを直すにはAが小さい。

D05は実runtimeが自己前提を無視することを実測したため、同期も自己辺だけを除外し複数ノード循環拒否は保つAが既存契約に一致する。新たなユーザー仕様選択とはしない。いずれもまだ実装していない。

## 保存互換の調査境界

削除7アイテムは実loaderで既存prune＋backup/pruned＋通知経路へ接続し、在庫はゲーム内から外れるが追跡可能な退避が残る。返金と同じではない。

削除超純水入りの合成saveはロード失敗、旧EUV第2枠へ鉄4個を埋めた合成saveは無ログ切捨てを再現した。ただし前者は旧生成元がなく、後者も素材数外slotを既存bindingが拒否するため、有効な旧出荷セーブからの到達を未証明。旧ブロック設置と任意スロット投入を混同しない。記録を保留し、実在経路の証明前に新しい移行・返金仕様をユーザーへ問わない。元セーブ38研究の最終master再計数は未了。

## Warning5件の行き先（黙って消さない）

- W01: 仮コスト判定が費用タプル一致を未確定状態の印にして誤判定し得る。現物誤拒否は未確認。bug-passへ渡す。
- W02: 任意param内GUID参照と族展開を使い道とみなし孤児を隠し得る。現物の具体的孤児は未確認。bug-passへ渡す。
- W03: fuelFluids先頭を燃料の同定に使う。現データは重水素と一致するがGUID確認の欠落をbug-passへ渡す。
- W04: sort独立算出/全周安定はTask19/23の既存原証拠で確認済み。新HEADでの全周再検証をpin更新前後にやり直す。
- W05: G1/G3/上位・混在の最終利用境界はTask24のpin98e0104原ログで確認済み。新HEADでのpin更新・再検証前に全体受入れとはしない。

## 原証拠・次の手

SDD workspace: `moorestech_logs/harness/sdd/feat-tier9-integrated/workspace/` のtask-23-fix-review、task-24-report/review/fix-report、task-24-master-validation-report、task-24-adjudication-reportと各evidence。

全体レビュー: `moorestech_logs/harness/moores-code-review/runs/2026-10-26-1759-tier9-master-edf1fb7f-c383-4746-8bec-4dddd9d144da/`。design.mdと追加のadjudication-reportを併読する。sdd-archiveでworkspaceを同期し、logsのcommitは既存hookの所有とする。

w7-s0はMASTERdb92f0aで取得済み（24b0605caa391bc14ee6c568a21bdbe636f1699e）。モデル/検査修正→関連回帰・全G6→反映差分Refix→bug-pass→独立評価2体→master PR→新pin QA再検証→Task24独立承認→J2の順。Surface隔離・未確定15件、J2 G4/G5、録画、元save、CI/paired PRも残る。規約の選択系統を独自の縮退レビューに置き換えない。
