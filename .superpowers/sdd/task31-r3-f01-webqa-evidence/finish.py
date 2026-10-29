import datetime
import hashlib
import html
import json
from pathlib import Path
import subprocess

BODY = Path('/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated')
QA = Path('/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech')
E = Path(__file__).resolve().parent
SHA = '7fd8f95723407b5d9418744078644ff3c6a498a5'


def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root, text=True)


def read(name):
    return json.loads((E / name).read_text())


rows = [read('results/' + p.name) for p in sorted((E / 'results').glob('*-*.json'))
        if p.name.startswith(('japanese-', 'english-', 'german-', 'korean-'))]
assert len(rows) == 48
assert read('results/matrix.json')['stats']['expected'] == 48
assert read('results/matrix.json')['stats']['unexpected'] == 0
assert read('results/playwright.json')['stats']['expected'] == 143
assert read('results/vitest.json')['numPassedTests'] == 1385
tracked_delta = git(BODY, 'diff', SHA, '--', '.', ':!.superpowers')
qa_status = git(QA, 'status', '--porcelain=v1')
assert tracked_delta == '' and qa_status == ''
owned = {p['pid']: p for row in rows for p in row['processes']}
for p in E.rglob('server-*.json'):
    entry = json.loads(p.read_text())
    owned[entry['pid']] = entry
for p in E.glob('*.json'):
    entry = json.loads(p.read_text())
    if isinstance(entry, dict) and 'ownerPid' in entry:
        for key in ['ownerPid', 'pid']:
            if key in entry:
                owned[entry[key]] = {'pid': entry[key], 'source': p.name}
processes = subprocess.check_output(['ps', '-axo', 'pid=,ppid=,comm='], text=True)
alive = {int(line.split()[0]): line for line in processes.splitlines()}
remaining = [alive[pid] for pid in sorted(owned) if pid in alive]
listeners = subprocess.run(['lsof', '-nP', '-iTCP:58391', '-iTCP:58392', '-sTCP:LISTEN'], text=True, capture_output=True)
unity = [line for line in processes.splitlines() if '/Unity.app/Contents/MacOS/Unity' in line]
audit = {'time': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'bodyHead': git(BODY, 'rev-parse', 'HEAD').strip(),
         'qaHead': git(QA, 'rev-parse', 'HEAD').strip(), 'bodyProductDiff': tracked_delta, 'qaStatus': qa_status,
         'ownedPids': sorted(owned), 'remainingOwnedProcesses': remaining, 'listenOutput': listeners.stdout,
         'listenExit': listeners.returncode, 'unityProcesses': unity, 'bodyStatus': git(BODY, 'status', '--porcelain=v1')}
(E / 'final-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2))
assert not remaining and listeners.returncode == 1 and not unity
summary = {'sha': SHA, 'vitest': {'files': 166, 'passed': 1385, 'failed': 0},
           'playwright': read('results/playwright.json')['stats'], 'matrix': read('results/matrix.json')['stats'],
           'minimumBodyCssHeight': min(r['scroller']['clientHeight'] for r in rows),
           'minimumBodyVisualHeight': min(r['scroller']['rect']['height'] for r in rows),
           'falseTransitionsPassed': sum(r.get('falseTransition', False) for r in rows),
           'consoleErrors': sum(len(r['consoleErrors']) for r in rows), 'pageErrors': sum(len(r['pageErrors']) for r in rows),
           'operationalDeviationCount': 1}
(E / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2))
checks = ['vitest', 'tsc-app', 'tsc-e2e', 'eslint-app', 'eslint-e2e-expanded', 'build', 'playwright', 'matrix']
table = ['| 検査 | 開始UTC | 終了UTC | exit |', '|---|---|---|---|']
for name in checks:
    m = read(name + '.json')
    table.append(f"| {name} | {m['start']} | {m['end']} | {m['code']} |")
commands = '\n'.join(json.dumps(read(name + '.json'), ensure_ascii=False) for name in checks)
(E / 'commands.jsonl').write_text(commands + '\n')
details = ['| 言語 | 台数 | 高さ | モード | 本文視口px | 末尾hit | 理由hit |', '|---|---|---|---|---|---|---|']
for r in rows:
    details.append(f"| {r['locale']} | {r['filters']} | {r['viewport']['height']} | {r['mode']} | {r['scroller']['rect']['height']:.2f} | {r['tail']['hit']} | {r['footerItems']['machine-wait-reason']['hit']} |")
report = f'''# Task31 r3 F01 独立Web QA

判定: 指定Web検査はPASS、製品修正を要する赤は0。隔離条件にはwiki照会による逸脱1件があるため「制約を完全順守したQA」とは報告しない。Unityを含む統合受入・r4目的評価の合否は未判定。

実行対象SHA: {SHA}。通常QA={QA}、証拠と報告のみBODYへ保存。BODYの製品差分0、通常QA tracked/untracked差分0。中間証拠commit=3f46602381aadc1b76bb6e68632524d8c6b61e91。最後に証拠のみ追加commitする。

全Vitest: 1385/1385 PASS、166ファイル、失敗0・skip0。旧6fの件数を転記せず今回のJSONから実数を回収した。既存Playwright: 143/143 PASS、skip0・flaky0。内訳: block 94（清浄機パネル19、清浄機多数13、清浄機レイアウト2、FilterSplitter3、machineRecipe10を含む）、inventory20、notification6、recipe18、uiState5。

app/e2e tscは各exit0。app既存ESLintはexit0。e2eは製品ESLint設定がsrcのみを対象とするため素の実行はexit2で全対象無視になった。製品設定を変更せずscratch設定で既存ルールのsrcセレクタだけをe2eへ写し、126ファイル・error0・warning0を検査した。parserのみの予備検査も別記録に残す。buildはexit0（既存の500kB chunk警告1件、1170モジュール）。

追加DOM検査: ja/en/de/ko × 清浄機20/30台 × 1280幅・600/720/1080高さ × インベントリ/レシピ選択の48条件すべてPASS。各条件で理由文・状態ラベル・電力率がパネル内か、本文の可視領域から離れているか、文字矩形が要素内か、中心hitできるかを確認。清浄機一覧最終行と本文最終行へスクロールしrect/hitを確認した。通常本文の最終行は分間生産数、レシピ選択は最終レシピ。true→falseで理由だけが消え待機ラベルが残る検査も48/48 PASS。最小本文高はCSS {summary['minimumBodyCssHeight']}px / 実画面 {summary['minimumBodyVisualHeight']:.2f}px、console/page error各0。CSS/pointer-events/トークンの書換え0。

mockを使った理由: nativeOwner不在・Unity起動禁止のため。通常QAのprodビルドと既存mock-hostを専用ポート58391/58392で配信し、scratch PlaywrightのWS受信変換だけでmachine.currentState=idle、outputSpaceShort=true/false、清浄機配列20/30台、ロケールを設定した。製品のZod入口・ストア・描画・既存MachineSection footerを通す。前後wireを各JSONへ保存。SubInventoryを明示し、既存辞書の見出しで背面レシピ面の不在も検査。画像48枚のうち4言語の低い画面を直接目視した。DEMO=1だが許可パスに実アイコンJPEGがないため既存placeholderを使用。実ゲーム画像・実C# publisherからの配信・遷移競合を保証する検査ではない。

初回失敗は検査環境に起因: 撮影用MOCK_DEMO=1を誤って全Vitestへ渡した結果、e2e/mock-host/tests/httpHandler.test.ts:42の[404,404]が[200,200]となり1件失敗。アサートを変えず通常環境で全1385件を再実行しPASS（初回ログ/JSONも保存）。e2e ESLint対象外exit2とscratch configのundefined ignoresによるexit2も保存。前者は既存設定の適用範囲、後者はscratchの設定ミスで修正済み。DOM予備検査では液体行から実末尾へ観測対象を補強し、撮影のPlayerInventory背面重複をSubInventory指定で解消した。予備48件の重複実行は受入件数に足していない。

運用逸脱: 開始時wiki-askを1回実行し、その照会子が禁止された共有main/master cloneのパスを検索する出力を観測した。ローカルQAの根拠には使わず追加照会を止め、所有PID 96732/96773/96778を確認してkillを試行した時点では既に終了していた。他セッションのwikiプロセスは停止していない。自分からの当該cloneへの追加アクセス・書換えはない。この逸脱を隠さず親へ返す。

未実施: Unity起動/UnityRunner/禁止送信テスト/課金API/常駐設定変更は今回のQAでは実行していない。既存Editorも最終確認0。Source compileError0は既存PID75254の証拠で、以後metaだけ7fdという事前情報に留める。Server3355・Save382・G1/G3・実Unity録画9は旧53/45の証拠で、最新7fdでは未実施。C#の理由計算と通知publisher変更があるため、その旧証拠を最新合格として再利用しない。旧6fの受入10PASSも最新成功に転記しない。目的の2独立評価r4は未開始、PR未作成。

終了確認: final-audit.jsonに所有PID・子プロセス・サーバー停止を記録。自作サーバーと記録した子PIDはすべて不在、両ポートLISTENなし。Playwrightが所有サーバーを終了する。共有サービス・他projectへ設定変更なし。scratchコードは各200行未満、rootのコードファイルは7本。

## 実行時刻

{chr(10).join(table)}

正確なコマンド引数、cwd、実行SHA、PID、開始終了はcommands.jsonlと検査別JSON。実結果はresults/vitest.json、results/playwright.json、results/matrix.json。全観測はresults/<locale>-<count>-<height>-<mode>.json、画像はimages/。

## DOM観測の全条件

{chr(10).join(details)}
'''
# ファイル提示用HTMLも同じ実測から作る。
# Build a file-sharing HTML view from the same measurements.
(BODY / '.superpowers/sdd/task31-r3-f01-webqa-report.md').write_text(report)
(E / 'report.md').write_text(report)
gallery = ''.join(f'<details><summary>{html.escape(r["name"])}</summary><img loading="lazy" src="images/{r["name"]}.png" style="max-width:100%"><p><a href="results/{r["name"]}.json">実DOM・wire JSON</a></p></details>' for r in rows)
(E / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>7fd Web QA</title><main style="max-width:1200px;margin:24px auto;font:16px/1.6 sans-serif"><h1>7fd 独立Web QA</h1><pre style="white-space:pre-wrap">' + html.escape(report) + '</pre>' + gallery + '</main>')
manifest = {str(p.relative_to(E)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(E.rglob('*')) if p.is_file() and p.name != 'sha256.json'}
(E / 'sha256.json').write_text(json.dumps(manifest, indent=2))
print(json.dumps(summary, ensure_ascii=False, indent=2))
