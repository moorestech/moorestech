# 記録の固定書式（pr-independent-review）

Step 7.5 / Step 8 / reconcile が書く成果物の書式。grep で横断集計するため見出し文言を生成ごとに変えない。
0件のセクションも省略せず「該当なし（0件）」の1行を置く（0件と収集し忘れを区別するため）。

## records/pr-<番号>.md（Step 8）

    # PR <番号> 独立レビュー

    - verdict: <Critical差し戻し|新形につき裁定行き|自動マージ可|未測定（スタブ）>
    - PRタイトル: <PRタイトル>
    - BASE_REF: <実値（式のまま。例: 8ce6f4dd...^1）>
    - 実施日: YYYY-MM-DD
    - checkout: <headRefOid一致|headRefOid不一致・mergeCommit検査>
    - 縮退: <なし（5系統フル実行）|<縮退内容。例: codex不在>|スタブ（Step 6未実行）>
    - head: <レビューしたHEADの40桁SHA>
    - base: <BASE_REFを解決した40桁SHA>
    - canonical: <$CANONのHEAD SHA><同一性ガードで差分が出たまま続行した場合のみ ・skew: $ORIGIN=<SHA> を追記>
    - 系統: <発火した系統名と各々の完了/縮退。例: 決定論=完了/レンズ3本=完了/reviewer5本=完了/Codex=縮退（不在）/Fable=完了>
    - session: <このレビューセッションの識別子>
    - rundir: <$LOGS/harness/pr-independent-review/ からの相対パス。例: runs/pr-1116/>

    ## 新形
    <新形フラグ1件1行（系統名・ファイル:行（lineがnullならファイルのみ）・要点）>
    - 系統別件数: new_edges（採用基準を満たすもの）N / asmdef_refs N / grammar N
    - 参考（新形に数えない）: generic_origin=false N件 / dir_is_new=true N件

    ## 裁定
    <裁定カード1件1行（ファイル:行・指摘要点・代替案）>

    ## suppressed
    <1件1行（ファイル:行・指摘要点・suppressed-by出所）>

- 測定器メタデータ行（head / base / canonical / 系統 / session / rundir）は省略禁止。`rundir` は「このverdictの実入力がどこにあるか」の唯一の口で、reconcile のフォレンジック・リプレイはここから辿る
- `head` と `base` は `git -C <$PRWTの実値> rev-parse HEAD` / `rev-parse "<BASE_REF>^{commit}"` の実出力、`canonical` は `git -C <$CANONの実値> rev-parse HEAD` の実出力（SHAピンなので clean/dirty の別は無い。`.last-used` は未追跡で dirty に数えない）
- 同一PRの再レビューは `pr-<番号>-r2.md`（以降 `-r3`…）を新規作成し、上書きしない（前回の見逃しを消すと見逃し率の実測が壊れる）

## shadow-ledger.md の1行（Step 8）

    | 日付 | PR番号 | head | verdict | 新形数 | suppressed数 | 縮退 | あなたの実判断（空欄） | 一致（空欄） | reconcile（空欄） |

- `head` は records の `- head:` の先頭7桁。同じPRを別headで再レビューした行を区別するため空欄にしない
- `縮退` は records の `- 縮退:` と同じ値。verdict を額面どおり見逃し率へ数えてよいかをこの列だけで判別する
- `reconcile` 列は reconcile モードだけが記入する（実施日 / `対象外（スタブ）`）。空欄＝突き合わせ未実施で、Step 0.5 の負債ゲートはこの列だけを見る
- 台帳は verdict 比較（`一致` 列）のままとし、欠陥単位の内訳は列にしない（ユーザー裁定 2026-08-02）。内訳は下の追補セクションへ

## 突き合わせ内訳（reconcile が records 末尾へ追記）

    ## 突き合わせ内訳（reconcile YYYY-MM-DD）

    ### caught
    <独立レビューが挙げ、人間も欠陥と認めたもの。1件1行（ファイル:行・要点）>

    ### missed
    <人間が欠陥と認めたが、独立レビューが挙げなかったもの。1件1行＋分類タグ＋コメントURL>

    ### false-positive
    <独立レビューが挙げたが、人間は欠陥と認めなかったもの。1件1行>

- 見逃し率は `missed / human-confirmed`（`human-confirmed` ＝ `caught` ＋ `missed`）。`false-positive` は分母に入れず別途の誤検知率
- 本セクションは reconcile 実施まで存在しないのが正（「0件でも省略しない」は本セクションには適用しない）

## findings.json（Step 7.5・コンバータ生成物）

裁定サイトと `pr-adjudicated-apply` の入力契約。`digest_build.py` が生成し、手で書かない・直さない。

```json
{
  "pr": <PR番号>,
  "head": "<レビューしたheadの40桁SHA（records の `- head:` と同値）>",
  "verdict": "<verdict判定規則で確定した最終verdict>",
  "generated_at": "<ISO8601>",
  "findings": [
    {
      "id": "F01",
      "title": "<指摘の一行タイトル>",
      "severity": "critical|high|medium|low",
      "category": "critical|design-decision|novelty",
      "files": ["path/to/file.cs:123"],
      "excerpt": "<問題箇所のコード抜粋>",
      "recommendation": "<推奨対応の要約>",
      "options": [
        {"key": "A", "summary": "<案Aの要約>", "recommended": true},
        {"key": "B", "summary": "<案Bの要約>"}
      ],
      "suppressed": false,
      "suppress_reason": ""
    }
  ]
}
```

- `recommended` は `options` の先頭に必ず付く。推奨案は digest.md の `options` 先頭に書くのが唯一の指定方法で、`recommended` キーを digest.md に書くとコンバータが落ちる
- id はコンバータが severity 降順→ファイルパス昇順→行番号昇順で `F01` から採番する。digest.md には id を書かず、相互参照は `[F:slug]` で書く
