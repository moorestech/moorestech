# バグ報告 manifest から prepare-run.sh が使う値を取り出す。読めない項目は空にし理由を stderr へ全部出す
# Extracts the values prepare-run.sh needs from a bug-report manifest; unreadable fields become empty and every reason goes to stderr
import json, shlex, sys

notes = []
data = {}
# 外部JSONのパースは外部境界。壊れた箱で run 全体を落とさないため例外をここで閉じる
# Parsing external JSON is a boundary; the exception is contained here so a broken box cannot kill the run
try:
    with open(sys.argv[1]) as handle:
        data = json.load(handle)
except Exception as error:
    notes.append("manifest.json を読めない（%s: %s）。全項目を空として続行する" % (type(error).__name__, error))
if not isinstance(data, dict):
    notes.append("manifest.json の中身が辞書でない。全項目を空として続行する")
    data = {}

def text(section_name, key):
    section = data.get(section_name)
    if not isinstance(section, dict):
        notes.append("manifest に %s が無い。%s.%s は空として続行する" % (section_name, section_name, key))
        return ""
    value = section.get(key)
    if isinstance(value, str) and value:
        return value
    notes.append("manifest の %s.%s が無い/空。空として続行する" % (section_name, key))
    return ""

# 再現は記録時と同じサーバーデータでしか成立しない。受け側は自分の worktree 配下へ解決するので相対表現を使う
# Reproduction holds only with the recording's own server data; the receiver resolves it under its own worktree, hence the relative form
server = data.get("serverData")
server_relative_to = ""
server_relative_path = ""
server_path = ""
if not isinstance(server, dict):
    notes.append("manifest に serverData が無い。記録時にサーバーが読んだマスタを特定できないため決定性検査は行えない")
else:
    server_relative_to = server.get("relativeTo") if isinstance(server.get("relativeTo"), str) else ""
    server_relative_path = server.get("relativePath") if isinstance(server.get("relativePath"), str) else ""
    server_path = server.get("path") if isinstance(server.get("path"), str) else ""
    if not server_relative_path:
        notes.append("serverData がリポジトリの外を指しているため受け側で解決できない: %s" % (server_path or "(path も空)"))

ticks = data.get("snapshotTicks")
if not isinstance(ticks, list):
    notes.append("manifest の snapshotTicks が無い/配列でない。スナップショット無しで続行する")
    ticks = []
numbers = [tick for tick in ticks if isinstance(tick, int) and not isinstance(tick, bool) and tick > 0]
if len(numbers) != len(ticks):
    notes.append("snapshotTicks に数値でない要素が混ざっている。数値だけを使う: %r" % (ticks,))
if not numbers:
    notes.append("snapshotTicks が空。固定ワールド起動はできないがログ・映像だけで続行する")
latest = str(max(numbers)) if numbers else ""

for item in data.get("missing") or []:
    if isinstance(item, dict):
        notes.append("報告側が欠損を申告している: %s（%s）" % (item.get("item"), item.get("reason")))
    else:
        notes.append("報告側が欠損を申告している: %r" % (item,))

values = [
    ("REPORT_COMMIT", text("repository", "commit")),
    ("REPORT_BRANCH", text("repository", "branch")),
    ("MASTER_COMMIT", text("masterData", "commit")),
    ("SERVER_DATA_RELATIVE_TO", server_relative_to),
    ("SERVER_DATA_RELATIVE_PATH", server_relative_path),
    ("SERVER_DATA_PATH", server_path),
    ("LATEST_TICK", latest),
]
# 値を全部取ってから理由を出す。取得中に増える note を取りこぼさないため
# Resolve every value first, then emit the notes, so notes added while resolving are not lost
for note in notes:
    sys.stderr.write("[prepare] %s\n" % note)
for name, value in values:
    print("%s=%s" % (name, shlex.quote(value)))
