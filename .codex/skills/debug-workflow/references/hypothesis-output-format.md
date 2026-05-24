# 仮説出力フォーマット仕様

全サブエージェントが従う出力スキーマ。SKILL.md の集約ロジックがこのフォーマットを前提に動くため、項目欠落は不可。

## 1 仮説あたりの必須項目

```markdown
### Hypothesis H{番号}
- **Severity**: Critical | Warning | Info
- **Category**: {自分の観点名 — 例: boundary-conversion}
- **Claim**: {一文で原因仮説。「〜のため〜が起きている」の形}
- **Evidence**:
  - `file/path.ext:行番号` — {その行が claim を支持する 1 行説明}
  - {引用は可能な限り file:line。grep 結果やコマンド出力の引用も可}
- **Recommended log placement (Step 4 用)**:
  - `file/path.ext:行番号` の{前 / 後}: `Debug.Log($"[DBG] {変数名}={値}")`
  - {仮説を検証するために仕込むべきログを具体的に。複数可}
- **Falsification**: {このログがこう出力されたらこの仮説は棄却される、と事前固定}
```

## Severity 判定基準

| Severity | 条件 |
|---|---|
| **Critical** | Evidence 3 件以上 + 既存成功経路との差分を引用に含む + Falsification 明確 |
| **Warning** | Evidence 2 件以上 + Falsification を明確に書ける |
| **Info** | Evidence 1 件以下、または Falsification が書けない、または明らかに観点周縁 |

Info も削除せず必ず出す (debug 用途では網羅性が優先する。Step 4 が空振った時の次候補として保持する)。

## 早期終了禁止

サブエージェントは **自分の観点が一見スコープ外でも、最低 1 件の仮説を必ず生成する**。`[applicable: no]` 出力は禁止。code-reviewer の skip / pass 慣習は採用しない。

観点が浅く適用できない場合は「もし無理にこの観点で説明するなら何が考えられるか」を Info で出す。

## 出力サンプル

```markdown
### Hypothesis H1
- **Severity**: Critical
- **Category**: boundary-conversion
- **Claim**: Physics.OverlapSphere の戻り値 Collider のアタッチ GameObject に TrainCarEntityObject が存在しないため、GetComponentInParent<TrainCarEntityObject> が null を返している
- **Evidence**:
  - `RideVehicleInputService.cs:49` — `_overlapBuffer[i].GetComponentInParent<TrainCarEntityObject>()` で取得しようとしている
  - `TrainCarObjectFactory.cs:57` — TrainCarEntityObject はルート GameObject にのみ AddComponent
  - `TrainCarObjectFactory.cs:73-74` — MeshCollider と TrainCarEntityChildrenObject は子の MeshRenderer GameObject に AddComponent (同じ GameObject)
  - 既存成功経路: `GameScreenSubInventoryInteractService.cs:42-44` は TrainCarEntityChildrenObject 経由で .TrainCarEntityObject を取得
- **Recommended log placement (Step 4 用)**:
  - `RideVehicleInputService.cs:49` の前: `Debug.Log($"[DBG] hit GO={_overlapBuffer[i].gameObject.name}, has TCEC={_overlapBuffer[i].GetComponent<TrainCarEntityChildrenObject>() != null}")`
  - `RideVehicleInputService.cs:49` の後: `Debug.Log($"[DBG] GetComponentInParent<TCE> result={car}")`
- **Falsification**: ログで `result=<some non-null TrainCarEntityObject>` と出力されたらこの仮説は棄却 (実際には GetComponentInParent が正しく親を辿れている)
```
