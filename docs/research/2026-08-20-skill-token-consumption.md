> **訂正（2026-08-20 再計測）**: 本書の集計スクリプトは `<session>/subagents/*.jsonl` を読んでおらず、消費の約63%を占めるサブエージェント分が欠落している。絶対額は実態の約1/3、`/pr-independent-review` の実シェアは約29%・完走1本は約$470〜540（Opus換算）。上限死の主因は poller の死因判定バグではなく「1本$500のレビューを同時起動数無制限で並走させる設計」と「sonnetオーケストレータの待機空転」。詳細は `2026-08-20-token-burn-reassessment.md` を正とする。

# スキル別トークン消費の実測（2026-08-06〜2026-08-20）

調査日: 2026-08-20 / 対象: `~/.claude/projects/**/*.jsonl` 全セッション（サブエージェント・headless含む）直近14日

## 要約

- 直近14日の総消費は **31.2億トークン**（Opus単価換算の概算 **$6,375**）。
- 単独スキル/コマンドの1位は **`/pr-independent-review` 19.9%**、2位が **`moores-code-review` 16.4%**（帰属定義の詳細は後述。実質は16〜20%）。
- **レビュー系ファミリー合計で約45%**（independent-review + code-review + adjudicated-apply + all-code-review）。直近2週間の消費のほぼ半分がレビューと裁定適用。
- `/pr-independent-review` の起動116件のうち **完走は13件（62.3%のトークン）**。残り **37.7%（概算$561）はverdictを1件も出さずに燃えた**。
- その一因は `pr-review/poller.py` の死因判定バグ（bd `moorestech-p99h`）。

## 1. 計測方法

### データ源

Claude Code のセッションtranscript `~/.claude/projects/<project>/<session-uuid>.jsonl`。
1行1レコードで、`type: "assistant"` のレコードが `message.usage` に
`input_tokens` / `cache_creation_input_tokens` / `cache_read_input_tokens` / `output_tokens` を持つ。

「トークン」= この4つの単純合計。`$`換算は Opus 単価（in $15 / out $75 / cache write $18.75 / cache read $1.5 per M）を全セッションに一律適用した**概算**（実際はサブエージェントに下位モデルが混ざるため上振れ側の見積り）。

### 帰属（どのメッセージをどのスキルに数えるか）

メッセージ列をセグメントに切り、各アシスタントメッセージのトークンを「その時点でアクティブなセグメントのラベル」へ加算する。**1トークンが2つのラベルに入ることはなく、合計はちょうど100%になる**（排他的分割）。

セグメントが始まる条件:
1. `Skill` ツール呼び出し → そのスキル名
2. `<command-name>/xxx` を含むユーザー発話 → `/xxx`

セグメントが終わる条件:
3. 次の `Skill` / コマンド
4. **素のユーザー発話**（`message.content` が文字列・`isMeta` 偽・sidechain 外）

`(通常対話)` は残余バケツ = 「スキルもコマンドも起動していない状態で流れたトークン」。セッション冒頭も既定でここに入る。

### 集計で踏んだ落とし穴（再現時に必ず効く）

| 罠 | 症状 | 対処 |
|---|---|---|
| `<task-notification>` を実ユーザー発話と誤認 | fan-out型スキルが**1/15に過少計上**（moores-code-review が 16.4% → 1.05% に化ける） | 区切りに使わない |
| `requestId` 重複レコード | 1リクエストが複数レコードに分かれる。dedupeで `continue` すると、その中の `tool_use` を見落としてスキル検知がほぼ全滅する | usage加算だけdedupeし、content走査は続ける |
| 同一セッションが複数ファイルに複製される | 総量が約5%二重計上される | `requestId` をファイル横断でグローバルにdedupe |
| `/model` `/effort` `/clear` 等のUIコマンド | これらがセグメント起点として拾われる | UIコマンド一覧を除外 |

## 2. スキル/コマンド別 消費（直近14日）

総計 3,117,997,658 tok / 概算 $6,375

| skill/command | tokens | tok% | 概算$ | cost% | 出力tok | 起動 |
|---|---:|---:|---:|---:|---:|---:|
| (通常対話) | 1,132.9M | 36.3% | 2,535 | 39.8% | 4.12M | – |
| /pr-independent-review | 619.0M | 19.9% | 1,295 | 20.3% | 2.11M | 116 |
| **moores-code-review** | **511.4M** | **16.4%** | **860** | **13.5%** | 0.51M | 16 |
| /pr-adjudicated-apply | 204.7M | 6.6% | 403 | 6.3% | 0.65M | 22 |
| subagent-driven-development | 166.1M | 5.3% | 318 | 5.0% | 0.60M | 14 |
| writing-plans | 105.3M | 3.4% | 235 | 3.7% | 0.69M | 17 |
| webui-design | 88.2M | 2.8% | 170 | 2.7% | 0.33M | 7 |
| pr-adjudicated-apply (skill) | 71.4M | 2.3% | 123 | 1.9% | 0.14M | 1 |
| ref-claude-tui-hook | 47.1M | 1.5% | 86 | 1.3% | 0.12M | 1 |
| uloop-launch | 45.4M | 1.5% | 79 | 1.2% | 0.10M | 1 |
| moores-grill-with-docs (+/形式) | 25.2M | 0.8% | 52 | 0.8% | – | 24 |
| pr-create | 19.7M | 0.6% | 34 | 0.5% | – | 11 |
| ref-ios-release | 14.1M | 0.5% | 31 | 0.5% | – | 4 |
| grilling | 14.0M | 0.5% | 35 | 0.6% | 0.12M | 18 |
| all-code-review | 11.0M | 0.4% | 28 | 0.4% | – | 2 |
| その他（domain-modeling, edit-schema, postmortem, artifact-design 等） | 計 約40M | 1.3% | – | – | – | – |

7日窓でもほぼ同じ（moores-code-review 17.3% / `/pr-independent-review` 17.5%）。

**レビュー系ファミリー合計**: `/pr-independent-review` + `moores-code-review` + `/pr-adjudicated-apply`(+skill) + `all-code-review` = **45.5%（トークン）/ 42.5%（コスト）**。

## 3. moores-code-review の実像

- 16回起動、**1回あたり約32Mトークン・概算$54**。
- 出力シェアは5.1%しかないのに消費シェアは16.4%。**cache read 約4.9億トークン**が支配的で、コストは「書く量」ではなく「サブエージェントに読ませる量」で決まっている。

### 帰属の上下振れ

`(通常対話)` の内訳を、セグメント内のツール呼び出しが `moores-code-review` を参照しているかで分解した:

| (通常対話) 1,156M の内訳 | セグメント数 | tokens | 通常対話内 |
|---|---:|---:|---:|
| 無関係 | 945 | 760.5M | 65.8% |
| スキル自体を読む/編集（スキル改修作業） | 63 | 277.5M | 24.0% |
| **Task/Agent でレビューを実走** | 14 | **118.4M** | 10.2% |

- **下振れ要因**: 自然言語依頼から `Skill` ツールを経ずにサブエージェントを撒いた 118.4M（全体の約3.8%）が `(通常対話)` に落ちている。合算すると **約20%**。
- **上振れ要因**: セグメントは次の実ユーザー発話まで続くため、レビュー後の修正・コミット・PR作成が同じラベルに残る。
- ユーザー発話で即打ち切る厳密版だと 1.0% まで落ちるが、レビュー中の「今どんな状況？」の一言で切れるため過小。
- **結論: 「レビュー起点の作業ブロック全体で16〜20%」が実態に最も近い。**

なお 24.0%（277.5M）の「スキル自体の改修」は、レビューの実行ではないので `(通常対話)` のままが妥当。ここを混ぜるとレビュー費用の意味が変わる。

## 4. `/pr-independent-review` 116起動の内訳

対象PRは15本なのに起動が116件と過大だったため個別に追跡した。

| 分類 | 件数 | tokens | 割合 | 概算$ | 1件平均 |
|---|---:|---:|---:|---:|---:|
| **完走**（verdict到達） | 13 | 365.5M | 62.3% | 694 | 28.1M |
| **上限死**（途中で session/weekly limit） | 12 | 112.8M | 19.2% | 300 | 9.4M |
| **自力停止・中断**（ガードでfail-closed / 質問待ち / 人が離脱） | 7 | 108.3M | 18.5% | 261 | 15.5M |
| **0トークン即死**（起動直後に上限/認証切れ） | 84 | 0 | 0% | 0 | – |

**verdictを1件も出さずに燃えたのが 221M トークン = 37.7%、概算$561**（完走レビュー約8本ぶん）。

0トークン即死84件の死因: `You've hit your session limit` 70 / `Failed to authenticate: OAuth session expired` 12 / `weekly limit` 2。

### PR別の無駄率

| PR | 総消費 | 完走分 | 無駄 | 起動内訳 |
|---|---:|---:|---:|---|
| 1145 | 26.7M | 0 | **100%** | 完走0・上限死1・空起動13 |
| 1179 | 5.4M | 0 | **100%** | 完走0・上限死1・空起動2 |
| 1178 | 11.4M | 1.5M | 87% | 完走1(公開工程のみ)・上限死2・空起動9 |
| 1140 | 30.0M | 4.2M | 86% | 完走1・上限死1・空起動2 |
| 1176 | 50.4M | 14.0M | 72% | 完走1・上限死2・空起動15 |
| 1189 | 57.2M | 27.9M | 51% | 完走1・上限死2・空起動15 |
| 1127 | 304.4M | 222.1M | 27% | 完走1・上限死1・空起動16 |
| 1171 / 1157 / 1154 / 1167 / 1138 / 1175 | 8.7〜19.3M | 全額 | 0% | 一発完走 |

PR1176の実際の経過: 20.2M走って上限死 → 12.7M走って中断 → 3.4M走って上限死 → 4本目の14.0Mでやっと完走。

## 5. 発見したバグ（bd `moorestech-p99h`）

`~/hermes-agent/data/services/pr-review/poller.py`（supervisor の periodic 120秒）:

`rate_limited()` が `state/pr-N/review.log` の**末尾10行**を `RATE_LIMIT_RE` で検査するが、**review.log は起動をまたいで追記され続ける**。数時間前の `session limit` の行が末尾10行に残る限り、実際の死因が別物でもレート制限と誤判定され、`RATE_LIMIT_BACKOFF_SECONDS=1800` でリトライ非消費のまま30分ごとに無限再起動する。

PR1145（2026-08-16）の実測:

```
17:42 launch → "session limit"（resets 8:10pm）
19:07以降    実際の死因は "OAuth session expired"（人間の再ログインが必要）
18:36〜23:42 「レート制限検知、リトライ回数を消費せず再起動」×12（30分ごと・全て0トークン即死）
23:42        OAuth行が11行たまり古いsession limit行を押し出す → 初めてクラッシュ判定
23:44,23:46  retry 1/2, 2/2 を消費
23:48        pr-1145 → 失敗（"質問/fail-closedの可能性" という誤った理由でPRコメント）
```

6時間無人で空転し、認証切れという本当の理由はDiscordにもPRコメントにも一度も出なかった。

### 修正方針

1. **判定を「直近1回の実行」に限定**する。launch毎に `review.log` をローテートするか、起動時のファイルオフセット以降だけを読む（本丸）
2. **`AUTH_RE` を独立分類**（`OAuth session expired` / `Failed to authenticate` / `Invalid API key`）→ 認証切れ状態としてDiscord通知＋長めバックオフ、リトライ非消費
3. **バックオフは reset 時刻を読む**。CLIが `resets 8:10pm` と報告しているのに固定1800秒で殴ると空起動が積む
4. **空起動がN回連続したら通知**（現状は無限に静かに空転。8/19の1176/1189は各17回）
5. **レビューを再開可能にする**。現状はレート制限死からの復帰が「同じレビューを最初から再実行」なので、20M級のコンテキスト構築を毎回捨てて再課金している。chunk/レンズ単位で成果物を永続化し、再起動時は未完了ぶんだけ走らせる

4までは空起動（$0）の是正だが、**実害の本体は 5 の「上限死12件・112.8M tok・概算$300」**である点に注意。

## 付録: 集計スクリプト

```python
import json, os, glob, datetime, collections, re
ROOT = os.path.expanduser("~/.claude/projects")
DAYS = 14
cut = datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=DAYS)
CMD = re.compile(r"<command-name>\s*/?([\w-]+)")
UI = {"model","effort","clear","compact","context","status","config","resume","cost","help"}
agg = collections.defaultdict(collections.Counter)
seen = set()  # requestIdはファイル横断で重複するためグローバルに排除する

for fp in glob.glob(os.path.join(ROOT, "*", "*.jsonl")):
    if datetime.datetime.fromtimestamp(os.path.getmtime(fp), datetime.timezone.utc) < cut:
        continue
    cur = "(通常対話)"
    for line in open(fp, errors="replace"):
        if not line.strip():
            continue
        try:
            r = json.loads(line)
        except Exception:
            continue
        t = r.get("timestamp")
        if t and datetime.datetime.fromisoformat(t.replace("Z", "+00:00")) < cut:
            continue
        m = r.get("message") or {}
        c = m.get("content")
        if r.get("type") == "assistant":
            rid = r.get("requestId") or r.get("uuid")
            if rid not in seen:                      # usage加算だけdedupeする
                seen.add(rid)
                u = m.get("usage") or {}
                a = agg[cur]
                a["in"] += u.get("input_tokens", 0)
                a["cc"] += u.get("cache_creation_input_tokens", 0)
                a["cr"] += u.get("cache_read_input_tokens", 0)
                a["out"] += u.get("output_tokens", 0)
            if isinstance(c, list):                  # content走査はdedupeの外で必ず回す
                for b in c:
                    if isinstance(b, dict) and b.get("type") == "tool_use" and b.get("name") == "Skill":
                        cur = (b.get("input") or {}).get("skill", "?")
        elif r.get("type") == "user":
            # task-notificationはシステム注入でユーザー発話ではないためリセットしない
            if isinstance(c, str) and not r.get("isMeta") and not r.get("isSidechain") \
               and "<task-notification>" not in c[:80]:
                mm = CMD.search(c)
                if mm and mm.group(1) in UI:
                    continue
                cur = ("/" + mm.group(1)) if mm else "(通常対話)"

def cost(v):
    return (v["in"]*15 + v["cc"]*18.75 + v["cr"]*1.5 + v["out"]*75) / 1e6

rows = sorted(((sum(v[k] for k in ("in","cc","cr","out")), k, v) for k, v in agg.items()), reverse=True)
g = sum(r[0] for r in rows)
for tot, k, v in rows:
    print(f"{k[:32]:32s} {tot:14,d} {100*tot/g:6.2f}% ${cost(v):9,.0f}")
```

セッション単位の分類（完走 / 上限死 / 自力停止 / 0トークン即死）は、各セッションの**最後のアシスタントtextブロック**を正規表現で判定した:

- 上限死: `hit your (session|weekly|usage) limit`
- 認証切れ死: `Failed to authenticate`
- 完走: `verdict|レビュー完了|独立レビュー完了|検査全通過|レビュー結果`
- それ以外: 自力停止/中断
