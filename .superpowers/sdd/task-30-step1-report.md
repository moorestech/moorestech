# Task30 Step1 報告

- ステータス: DONE
- 対象: セッションA Task30 Step1のみ。Task30 Step2以降・Task4以降は未着手。
- 台帳: moorestech-g1rl5
- 作業cwd: /Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated

## 実施内容

指定契約→ブリーフ→global constraintsの順に読み、logs README・関連裁定・レビュー規約を確認した。
取得済み原本3ファイルの構成とSHAを確認し、同じ親のtier8-worldへ3ファイルだけコピーした。
原本は読み取りのみ。devmacへの接続・変更は行っていない。

コピーのsave.jsonに研究7件（リセット対象6件＋旧ロケット）を各1回追加し、
指定ブロック3件・表示8件を各1件かつisUnlocked=trueに設定した。
worldVersionは6のまま。移行・Unityロード・ビルド・録画は本Stepの対象外であり未実行。

元38研究の欠落集計には、現在の本体ピンで利用可能なマスタのコミット
630fdce08eccee47feaa15721ca607f407000ab9をgit showで固定して参照した。
欠落0件・残存38件は暫定値であり、W1作業中の統合マスタや最終値を示していない。
W2後、最終統合マスタのpush済みコミットを固定して再集計することが必須。
Bのマスタworktreeは変更していない。

## 変更ファイル

- 外部scratch: /Users/sakastudio/hermes-agent/data/repos/moorestech_logs/.state/tier9-integration/tier8-world/{save.json,map.json,world.json}
- 出所文書: /Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/tier9-integration/handoff/save-origin.md
- 本報告: .superpowers/sdd/task-30-step1-report.md

scratchのsave.jsonのみ原本から内容変更。map.json/world.jsonはバイト一致のコピー。
セーブ本体はコミットしない。logs成果物はhook所有のため手動コミットしない。
本体のコード・設定・マスタの変更はない。既存の2件のplan変更は他作業として保持。

## 検証コマンド・出力

以下は実際に実行した一回限りのコピー編集・全件検証コマンド。
再実行時は既存コピーの上書きを防ぐassertで停止する。再利用用スクリプトファイルは追加していない。

```bash
python3 - <<'PY'
import copy, hashlib, json, shutil, subprocess
from pathlib import Path

root = Path('/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/.state/tier9-integration')
original, target = root/'tier8-world-original', root/'tier8-world'
logs = root.parents[1]
expected_sha = '454bab3830ca910f688598f16b787886927e33d4d81ecd78497cc891cb02c9c0'
master_commit = '630fdce08eccee47feaa15721ca607f407000ab9'
master_path = 'server_v8/mods/moorestechAlphaMod_8/master/research.json'
research = [
    '019e3aab-32c6-7166-a184-d761153c2498', '019e3ae2-5005-7439-b242-9b7b3191a2c3',
    '019e3af5-f2e6-76de-a80f-5e66e79fa35d', '019e3af0-297f-726e-8a02-0a8518124e6d',
    '019e3aef-a149-746b-8fad-159e0434cb51', '01a16291-4094-7369-a87f-5dfe2197d25e',
    '019e3af0-7b1c-77de-9197-87d276c84392']
blocks = [
    '01a162f1-ab04-76e2-94cf-dc713edeb63b', '01a162aa-cec0-738c-93c5-f58adc87fa7f',
    '01a162b0-7769-7419-af24-bb35528b93d2']
views = [
    '01a162ab-07ba-73c1-9dd6-38e1e867663b', '019e3af5-e750-71ae-ab09-2b98f3db9913',
    '01a16290-f793-748b-a0b0-91d44be0d4ab', '01a16297-4724-73b9-82c3-2e77120be77d',
    '01a16297-6665-765d-8e19-96d357394c3e', '01a16297-a7f5-759b-91ba-be4021ca3bf2',
    '01a162a4-9334-7286-a8ba-7daf9c0f407a', '01a162a8-e435-73d0-bddb-08533819b80e']
names = ['map.json', 'save.json', 'world.json']
assert sorted(p.name for p in original.iterdir()) == names
before = {n: (original/n).read_bytes() for n in names}
assert hashlib.sha256(before['save.json']).hexdigest() == expected_sha
source = json.loads(before['save.json'])
assert source['worldVersion'] == 6
completed = source['research']['CompletedResearchGuids']
assert len(completed) == len(set(completed)) == 38
assert not set(research).intersection(completed)
master_raw = subprocess.check_output([
    'git', '-C', '/Users/sakastudio/hermes-agent/data/repos/moorestech_master',
    'show', master_commit + ':' + master_path])
master = json.loads(master_raw)['data']
master_guids = {r['researchNodeGuid'] for r in master}
missing = len(set(completed) - master_guids)
assert not target.exists(), 'Existing destination requires inspection; refusing overwrite'
shutil.copytree(original, target)
modified = copy.deepcopy(source)
modified['research']['CompletedResearchGuids'].extend(research)
for key, guids in [('blockUnlockStateInfos', blocks), ('itemUnlockStateInfos', views)]:
    rows = modified['gameUnlockState'][key]
    for guid in guids:
        matches = [r for r in rows if r['guid'] == guid]
        assert len(matches) <= 1
        if matches:
            matches[0]['isUnlocked'] = True
        else:
            rows.append({'guid': guid, 'isUnlocked': True})
(target/'save.json').write_text(json.dumps(modified, ensure_ascii=False), encoding='utf-8')

# Read back and assert every intended entry and every untouched field.
actual = json.loads((target/'save.json').read_bytes())
assert actual == modified
assert actual['worldVersion'] == 6
assert len(actual['research']['CompletedResearchGuids']) == 45
for guid in research:
    assert actual['research']['CompletedResearchGuids'].count(guid) == 1
restored = copy.deepcopy(actual)
restored['research']['CompletedResearchGuids'] = [
    g for g in restored['research']['CompletedResearchGuids'] if g not in research]
for key, guids in [('blockUnlockStateInfos', blocks), ('itemUnlockStateInfos', views)]:
    old = source['gameUnlockState'][key]
    rows = actual['gameUnlockState'][key]
    for guid in guids:
        matches = [r for r in rows if r['guid'] == guid]
        assert len(matches) == 1 and matches[0]['isUnlocked'] is True
    original_ids = {r['guid'] for r in old}
    restored['gameUnlockState'][key] = [
        next(r for r in old if r['guid'] == row['guid']) if row['guid'] in guids else row
        for row in rows if row['guid'] not in guids or row['guid'] in original_ids]
assert restored == source
assert sorted(p.name for p in target.iterdir()) == names
for name in names:
    assert (original/name).read_bytes() == before[name]
    if name != 'save.json':
        assert (target/name).read_bytes() == before[name]
after_sha = hashlib.sha256((target/'save.json').read_bytes()).hexdigest()
print('PASS original SHA256:', expected_sha)
print('PASS original unchanged: 3/3 files')
print('PASS scratch contains only save.json/map.json/world.json')
print('PASS research complete: 7/7 exactly once; total 38 -> 45')
print('PASS block unlock: 3/3 exactly once and isUnlocked=true')
print('PASS item view unlock: 8/8 exactly once and isUnlocked=true')
print('PASS worldVersion: 6 -> 6')
print('PASS all non-target JSON fields unchanged')
print('PASS map.json/world.json byte-identical: 2/2')
print('modified save SHA256:', after_sha)
print('provisional master commit:', master_commit)
print('research.json SHA256:', hashlib.sha256(master_raw).hexdigest())
print('provisional missing original research:', missing, '/ 38')
print('provisional surviving original research:', 38-missing, '/ 38')
print('REQUIRED: recount with final integrated master after W2; this is not a final value')
origin = logs/'harness/tier9-integration/handoff/save-origin.md'
assert not origin.exists(), 'Refusing overwrite of existing provenance document'
origin.parent.mkdir(parents=True, exist_ok=True)
lines = [
    '# Task30 Step1 セーブ出所・検証',
    '',
    '出所: 取得済みMacBook Proワールドの原本3ファイル。リモート変更なし。',
    '原本: .state/tier9-integration/tier8-world-original/{save.json,map.json,world.json}',
    '編集コピー: .state/tier9-integration/tier8-world/{save.json,map.json,world.json}',
    'セーブ本体は非公開scratch内のみ。コミット禁止。',
    '',
    '## SHA256',
    *[f'- 原本 {n}: {hashlib.sha256(before[n]).hexdigest()}' for n in names],
    f'- 編集後 save.json: {after_sha}',
    '- 原本3ファイル不変。コピーのmap.json/world.jsonは原本とバイト一致。',
    '',
    '## 件数・版',
    '- worldVersion: 6のまま。移行処理は未実行。',
    '- 元の完了研究38件。指定7件追加後45件（各1回）。ブロック3件・表示8件は各1件かつisUnlocked=true。',
    '- 指定箇所以外のJSON値は全件不変。',
    '',
    '## 指定GUID',
    '研究7件（末尾は旧ロケット）:',
    *['- '+g for g in research],
    'ブロック3件:',
    *['- '+g for g in blocks],
    '表示8件:',
    *['- '+g for g in views],
    '',
    '## 元38研究の欠落集計（暫定）',
    f'- 参照マスタコミット: {master_commit}（現在の本体ピン。W1作業中の統合マスタではない）',
    f'- 参照ファイル: {master_path}',
    f'- 参照ファイルSHA256: {hashlib.sha256(master_raw).hexdigest()}',
    f'- 元38件のうち欠落: {missing}件。残存: {38-missing}件。',
    '- 最終値ではない。W2後、最終統合マスタのpush済みコミットを指定して再集計必須。',
    '- 上記残存件数は元38件のみの照合値。追加7件の移行後残存を示す値ではない。',
]
origin.write_text('\n'.join(lines)+'\n', encoding='utf-8')
print('PASS provenance written without private save payload or original completed GUID list')

PY
```

出力（exit 0）:

```text
PASS original SHA256: 454bab3830ca910f688598f16b787886927e33d4d81ecd78497cc891cb02c9c0
PASS original unchanged: 3/3 files
PASS scratch contains only save.json/map.json/world.json
PASS research complete: 7/7 exactly once; total 38 -> 45
PASS block unlock: 3/3 exactly once and isUnlocked=true
PASS item view unlock: 8/8 exactly once and isUnlocked=true
PASS worldVersion: 6 -> 6
PASS all non-target JSON fields unchanged
PASS map.json/world.json byte-identical: 2/2
modified save SHA256: e72884ab1dfe811582ba89e9be8e8b0263f3b708863f22cc1b9b8201e1a30ac5
provisional master commit: 630fdce08eccee47feaa15721ca607f407000ab9
research.json SHA256: cf5130ccde5934c599ab4b94e1a57af20a78e4d42ae244795be04ded1466914b
provisional missing original research: 0 / 38
provisional surviving original research: 38 / 38
REQUIRED: recount with final integrated master after W2; this is not a final value
PASS provenance written without private save payload or original completed GUID list
```

別のコマンドでディスクから再読み込みし、文書の指定GUID一覧と照合した:

```bash
python3 - <<'PY'
import hashlib, json, re, subprocess
from pathlib import Path
root = Path('/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/.state/tier9-integration')
original = root/'tier8-world-original'
target = root/'tier8-world'
assert hashlib.sha256((original/'save.json').read_bytes()).hexdigest() == '454bab3830ca910f688598f16b787886927e33d4d81ecd78497cc891cb02c9c0'
assert hashlib.sha256((target/'save.json').read_bytes()).hexdigest() == 'e72884ab1dfe811582ba89e9be8e8b0263f3b708863f22cc1b9b8201e1a30ac5'
source = json.loads((original/'save.json').read_bytes())
actual = json.loads((target/'save.json').read_bytes())
origin = root.parents[1]/'harness/tier9-integration/handoff/save-origin.md'
text = origin.read_text()
sections = [text.split('研究7件（末尾は旧ロケット）:\n')[1].split('ブロック3件:')[0],
            text.split('ブロック3件:\n')[1].split('表示8件:')[0],
            text.split('表示8件:\n')[1].split('## 元38研究')[0]]
groups = [re.findall(r'^- ([0-9a-f-]{36})$', s, re.M) for s in sections]
assert list(map(len, groups)) == [7, 3, 8]
assert len(set(re.findall(r'\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b', text))) == 18
for guid in groups[0]:
    assert actual['research']['CompletedResearchGuids'].count(guid) == 1
for key, guids in zip(['blockUnlockStateInfos', 'itemUnlockStateInfos'], groups[1:]):
    for guid in guids:
        matches = [r for r in actual['gameUnlockState'][key] if r['guid'] == guid]
        assert len(matches) == 1 and matches[0]['isUnlocked'] is True
assert source['worldVersion'] == actual['worldVersion'] == 6
assert len(source['research']['CompletedResearchGuids']) == 38
assert len(actual['research']['CompletedResearchGuids']) == 45
for name in ['map.json', 'world.json']:
    assert (original/name).read_bytes() == (target/name).read_bytes()
master = json.loads(subprocess.check_output(['git', '-C',
    '/Users/sakastudio/hermes-agent/data/repos/moorestech_master', 'show',
    '630fdce08eccee47feaa15721ca607f407000ab9:server_v8/mods/moorestechAlphaMod_8/master/research.json']))
assert len(set(source['research']['CompletedResearchGuids']) - {r['researchNodeGuid'] for r in master['data']}) == 0
print('PASS independent readback: original/copy SHA, 7+3+8 entries, worldVersion6, 38->45, map/world bytes, provisional missing=0')
print('PASS provenance GUID inventory: exactly the specified 18 GUIDs; no original completed GUID inventory')

PY
```

出力（exit 0）:

```text
PASS independent readback: original/copy SHA, 7+3+8 entries, worldVersion6, 38->45, map/world bytes, provisional missing=0
PASS provenance GUID inventory: exactly the specified 18 GUIDs; no original completed GUID inventory
```

## 自己レビュー

- 書き戻したJSONを再読し、指定7+3+8件を全件検証した。
- 指定変更だけを逆適用したJSONが原本と完全一致することを検証し、他フィールドの混入変更を排除した。
- 原本3ファイルは開始時バイト列との一致で不変を確認した。
- 出所文書はSHA・指定18GUID・件数・参照元情報のみ。個人セーブのペイロードや元38研究のGUID一覧は含めない。
- .cs変更なしのためコンパイルは対象外。ゲームでの移行成功を検証したという主張はしていない。
- Task30 Step1は完了。W2後の最終マスタ再集計は後続担当へ引継ぎ。

