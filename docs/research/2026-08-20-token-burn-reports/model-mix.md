# モデル構成 × 役割 集計（直近5日）

集計元: `recs.pkl`（46,892 assistantメッセージ）。役割はサブエージェント transcript の最初のuserメッセージ冒頭パターンで分類（`role1.py`）。Opus換算$ = `(in*15+cc*18.75+cr*1.5+out*75)/1e6`。実単価$は role1.py の RATES（fable未公表のためopusと同額で仮置き・注記）。

## 役割 × モデル 集計（opus$降順、上位）

| role | model | opus$ | 実単価$ | n(msgs) | sample sid |
|---|---|---:|---:|---:|---|
| main | opus | 4178.3 | 4178.3 | 10804 | fe8e6fb1 |
| reviewer | opus | 2237.9 | 2237.9 | 10620 | agent-aa |
| オーケストレータ | sonnet | 1464.6 | 292.9 | 3964 | agent-a3 |
| implementer | sonnet | 898.8 | 179.8 | 4060 | agent-a3 |
| その他 | opus | 658.4 | 658.4 | 3287 | agent-a2 |
| main | fable | 610.2 | 610.2* | 1643 | 6d598306 |
| implementer | opus | 489.5 | 489.5 | 1997 | agent-a4 |
| investigator | sonnet | 372.4 | 74.5 | 1648 | agent-af |
| レンズ | opus | 304.9 | 304.9 | 1479 | agent-af |
| その他 | sonnet | 299.6 | 59.9 | 1274 | agent-a5 |
| reviewer | sonnet | 295.3 | 59.1 | 1063 | agent-a4 |
| integrator | opus | 256.0 | 256.0 | 700 | agent-a5 |
| その他 | fable | 237.6 | 237.6* | 871 | agent-a3 |
| レンズ | fable | 119.9 | 119.9* | 473 | agent-a1 |
| レンズ | sonnet | 100.4 | 20.1 | 441 | agent-ac |
| digest | sonnet | 86.3 | 17.3 | 321 | agent-a0 |
| fix | sonnet | 60.2 | 12.0 | 278 | agent-ae |
| investigator | opus | 46.3 | 46.3 | 216 | agent-a0 |
| uloop/テスト | opus | 45.3 | 45.3 | 226 | agent-ae |
| simulator | opus | 44.9 | 44.9 | 252 | agent-ae |
| fix | opus | 33.6 | 33.6 | 151 | agent-a6 |
| codex | opus | 7.2 | 7.2 | 35 | agent-a3 |

\* fable単価は不明のため opus 同額で仮置き（実際はopusより安い可能性が高く、この報告のfable分は過大見積り）。

合計: opus換算 **$13,006**（既知の総額$14.5kとほぼ整合、差分は分類漏れの`その他(不明)`・`haiku`小口）。実単価換算合計 **$10,066**（fable仮置き分を除けばさらに下がる）。

## opus→sonnet置換候補と根拠

`orchestrator-steps.md`・`lenses/*.md`・`scripts/model_map.json`を確認した結果、**verifier/investigatorは既にsonnetへ降格済み**（2026-08-16裁定）。reviewerはmodel_map.jsonの`default: opus`で8本だけがsonnet許可（bug狩りの推論品質を優先する意図的設計）。integrator/comment-rationale-guardも「WHY判定・高ステークス」を理由に明示的にopus固定。

機械的・小粒でsonnet代替が妥当な部分（fix, digest, uloop/テスト, simulator, investigator残存分, codex）を合算すると **opus$193.4** 相当。これをsonnet実単価（opus比で入力1/5・出力1/5相当、実測比率≈0.20）に置き換えると実費**約$39**まで下がり、**約$154の削減**（5日間で）。同義に「その他」opus$658.4のうち`調査してください`型のExplore的タスク（サンプル確認済み、`Explore`ラベルに正規表現がマッチしていない誤分類）が数百$含まれており、これも置換候補として別途精査の価値あり。

## main（親セッション）モデル別

| 系統 | セッション数 | opus$計 | 平均$/セッション |
|---|---:|---:|---:|
| opus | 119 | 4161.7 | 35.0 |
| fable | 25 | 617.8 | 24.7 |
| sonnet | 5 | 4.4 | 0.9 |
| haiku | 2 | 16.2 | 8.1 |

fableで走った親セッションは25本・合計$617.8（opus換算、fable実単価は不明のため上振れの可能性）、1本平均$24.7。
