# Tier9: 検証コードの修正と質問撤回

## 判断の訂正

ユーザーの「何の話？バランス調整の話？」「やらない理由無く何？なにが意思決定の軸なの？」という指摘を受け、controllerが内部の実装方法を人間の設計選択へ上げた質問①②を撤回した。これはユーザーがAを選んだという裁定ではない。既存仕様・費用・目標で定まる検証コードの不具合はcontroller既定で修正する。ゲーム内数値や目標変更が必要と実測された時だけ相談する。既存SDDの質問振分け規則からの逸脱であり、新規規則・hookを足す対応はしていない。

## 確定した修正（MASTER）

BASE db92f0afdf271663316204b334ae81eefbeae553から、モデル/検査修正を5e8a67417f8eded9d7dcf3913346afc025bab77aまで実装し、独立SDDレビューでSpec適合/Approvedを得た。反映diffレビューもCritical/Warning0で収束。さらに構造レビューで見つけた実行可否3条件の重複を744e2f102c8c72b75521d07ed4b4efcc8f4483bcで共通化し、その反映も独立検査Critical/Warning0で確認した。すべてpush済み、remoteSHA一致/clean。

- D02: 爆弾の確定1個出力を照合し、数量・確率・欠落が不正な入力を理由付きで止める。
- D03: 祖先集合は起点ごとに完全探索し、配列順依存の部分集合キャッシュを除去。
- D04: recipe実行可否を到達計算とdeadRecipes監査で共用。前提失敗理由の3条件も一箇所へ集約。
- D05: 実runtimeへ整合させ自己前提のみ無視、未知参照・複数循環は拒否。
- D01/C06: 実台数×ライン分のモジュールを払って製作し、実装着後だけ効果を反映。増設や在庫再利用を数量保存。
- C08: S1終点の設備/在庫/注文をS2へ渡し、研究Tier・既存stack3/6/10・GPU/衛星加工・発射サイクル・前段在庫を接続。
- 非chip材料の未払い、ロケット技術と発射台の無料開始を独立レビューで追加再現し、同じ直接部材台帳から支払って準備するよう修正。
- 供給源の開始時解放、S1未完の副作用0停止、S1途中の研究を開始completedへ誤算入するケースを回帰で修正。
- 直GPUレシピの未unlock使用を修正。既存/追加のResourceWarningはwithでcloseし0件。

## 数値はゲーム調整ではなく、再計算したsim結果

| 区間 | 時間 |
|---|---:|
| S1 A/B/C | 6.3333 / 6.6667 / 7.3333 h |
| S2（Bから継続） | 5.158333 h |
| S3 | 5.229167 h |
| Tier9合計 | 17.054167 h |

S2の旧4.5hは120便×135秒だけの理想時間だった。新値は準備/材料/製作待ちを含む。目標範囲はS1 6〜8、S2 4〜6、S3 5〜11、全体15〜25hのまま。研究費150/300/600/1200、出荷JSON/spec/GUID台帳、レシピ/品質/stack、固定factory定数、G2許可19件は変更していない。

これはreference factoryと定常上流/建設時間を前提にした期待値モデルであり、実プレイ時間の証明ではない。直接部材台帳は本区間への取り分で、上流の同名品目の全生産・全在庫や建物の原始資材を表すものではない。実プレイの供給充足は未検証で、モデル修正をその証拠に流用しない。

## 検証と証拠

- 独立SDD最終レビュー: `.superpowers/sdd/task-24-model-fix4-review.md`。先のI1/I2/I3/I4/I3bとM1を閉鎖。実装者のDONEを承認に使っていない。
- Refix: 主reviewの `agents/refix-correctness-w7-fix3-r2.md`、`agents/refix-correctness-w7-structure-fix-r1.md`。第三の修正反映で出たI3bは直し直し2/3周目で収束。前の反映2枠は各Critical0で終了済み。
- 最終HEAD5e8a674/cleanでcontrollerが全関連検証を再実行し、dirty時ログとcommitの紐付けの懸念を解決。tier9=115、progression=56、nodegraph=10、research_sync=7、apply=6すべてOK。G2許可19でPASS、G6=0/398、sync/ngも成功。
- 同じ最終HEADで一時コピーへapplyし、10spec/新GUID0、全252modファイルとGUID台帳がbyte一致。14masterJSONのSHA256も記録した。一時コピーは削除済み。
- 構造共通化744e2f1でも関連回帰とG2を成功確認。C#変更0でEditor不使用。反映の録画シナリオ/SaveLoad機械確認は対象外。
- 原証拠: logsの `harness/sdd/feat-tier9-integrated/workspace/task-24-model-*`、`task-24-structure-*`。特にfix4-evidenceの100-final-head-*と101-final-head-apply.txt。

## 未完了（今回の修正で免除しない）

BODYはMASTER98e0104にpinしたままで、新MASTERへのpin更新・G1/G3等再受入れはまだ。Task24/masterPR/J2は未完了。master全体bug-pass、目的のみの独立評価2体、Surface15未確定（共有cache/共通XMLの隔離が必要）、J2 G4/G5、録画、元save38研究再計数、最終CI/paired PRが残る。共有clone incidentと過去ログの未同定Errorも別件のまま。

主review RUNDIR: `harness/moores-code-review/runs/2026-10-26-1759-tier9-master-edf1fb7f-c383-4746-8bec-4dddd9d144da/`。Step7で却下した指摘は0件（ユーザーの却下を捏造しない）。最終bug-passは元base b2149a0からのmaster全branch差分を入力とし、従来のWarning・構造レビューWarning5件も持ち込む。
