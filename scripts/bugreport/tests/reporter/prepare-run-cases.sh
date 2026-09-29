# test-prepare-run.sh の一時リポジトリを使い、複製セーブだけが変更されることを検査する
# Use test-prepare-run.sh's temporary repositories to check that only the copied save changes
for reporter_case in steam device missing unmatched device-unmatched unbound version2; do
  reporter_run="$TMP/runs/reporter-$reporter_case"
  mkdir -p "$reporter_run/snapshots"
  python3 - "$reporter_run" "$REPORT_COMMIT_1B" "$reporter_case" <<'PY'
import json, pathlib, sys
run, commit, case = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3]
manifest = {"repository": {"commit": commit}, "snapshotTicks": [300]}
if case in ("steam", "version2"):
    manifest["steamId"] = "76561198319362448"
elif case == "unmatched":
    manifest["steamId"] = "999"
elif case in ("device", "unbound"):
    manifest["deviceIdentity"] = "device:" + "a" * 64
elif case == "device-unmatched":
    manifest["deviceIdentity"] = "device:" + "c" * 64
entries = [{"playerId": 3, "identity": None},
           {"playerId": 2, "identity": "steam:76561198319362448"},
           {"playerId": 4, "identity": "device:" + "b" * 64},
           {"playerId": 1, "identity": "device:" + "a" * 64}]
if case == "unbound":
    for entry in entries:
        entry["identity"] = None
save = {"currentTick": 300, "players": {"nextPlayerId": 5,
        "claimCandidatePlayerId": 3, "entries": entries}, "playerInventory": [
    {"PlayerId": 1, "MainInventoryItems": [{"count": 7}],
     "EquipmentInventoryItems": [{"count": 8}], "GrabInventoryItems": {"count": 9}},
    {"PlayerId": 2, "MainInventoryItems": [{"count": 1}]},
    {"PlayerId": 4, "MainInventoryItems": [{"count": 20}]},
    {"PlayerId": 3, "MainInventoryItems": [{"count": 1000}]}]}
if case == "version2":
    save["worldVersion"] = 2
    del save["players"]
(run / "manifest.json").write_text(json.dumps(manifest))
(run / "snapshots/tick_300.json").write_text(json.dumps(save))
PY
  env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" "reporter-$reporter_case" 2>"$TMP/reporter-$reporter_case.log"
  (
    source "$reporter_run/run.env"
    if [[ "$reporter_case" == "steam" || "$reporter_case" == "device" ]]; then
      [ "$REPORTER_UNCLAIM_FAILED" = "0" ] || { echo "NG: 付け替え成功が失敗扱い: $reporter_case"; exit 1; }
    else
      [ "$REPORTER_UNCLAIM_FAILED" = "1" ] || { echo "NG: 付け替え未了のフラグが無い: $reporter_case"; exit 1; }
    fi
    python3 - "$WORLD_DIR/save.json" "$reporter_run/snapshots/tick_300.json" "$reporter_case" <<'PY'
import json, sys
save, original = [json.load(open(path)) for path in sys.argv[1:3]]
case = sys.argv[3]
expected = json.loads(json.dumps(original))
if case in ("steam", "device"):
    selected_id = 2 if case == "steam" else 1
    expected["players"]["claimCandidatePlayerId"] = selected_id
    next(entry for entry in expected["players"]["entries"]
         if entry["playerId"] == selected_id)["identity"] = None
assert save == expected, (case, save, expected)
if case != "version2":
    assert original["players"]["claimCandidatePlayerId"] == 3
    assert case == "unbound" or original["players"]["entries"][1]["identity"] is not None
else:
    assert original["worldVersion"] == 2 and "players" not in original
PY
  )
  case "$reporter_case" in
    steam) reporter_message="プレイヤー2" ;;
    device) reporter_message="プレイヤー1" ;;
    missing) reporter_message="steamId も deviceIdentity も無い" ;;
    unmatched|device-unmatched|unbound) reporter_message="結びつくプレイヤーが無い" ;;
    version2) reporter_message="報告者と一致する保証なし" ;;
  esac
  grep -q "$reporter_message" "$TMP/reporter-$reporter_case.log" || { echo "NG: 報告者の付け替え理由が無い: $reporter_case"; exit 1; }
done

# 壊れた外部入力は複製セーブを変えず、理由を残して再現準備を続ける
# Corrupt external input leaves the copied save unchanged and logs why preparation continues
python3 - "$HERE/../unclaim-reporter.py" "$TMP" <<'PY'
import json, pathlib, subprocess, sys
script, root = sys.argv[1], pathlib.Path(sys.argv[2])
save_path, manifest_path = root / "invalid-save.json", root / "invalid-manifest.json"
manifest_path.write_text(json.dumps({"deviceIdentity": "device:" + "a" * 64}))
for source in ("{broken", "[]", '{"players": []}',
               '{"players":{"entries":[null]}}',
               json.dumps({"players": {"entries": [{"playerId": 0, "identity": "device:" + "a" * 64}]}})):
    save_path.write_text(source)
    result = subprocess.run(["python3", script, str(save_path), str(manifest_path)], capture_output=True, text=True)
    assert result.returncode != 0, result.stderr
    assert result.stderr and "Traceback" not in result.stderr, result.stderr
    assert save_path.read_text() == source

# manifestが読めなければ報告者を決められない。推測で他人を持ち主未定へ戻さない
# An unreadable manifest names no reporter, so no guess sends another player back to unclaimed
manifest_path.write_text("{broken")
intact = json.dumps({"players": {"nextPlayerId": 2, "claimCandidatePlayerId": None,
                                 "entries": [{"playerId": 1, "identity": "device:" + "a" * 64}]}})
save_path.write_text(intact)
result = subprocess.run(["python3", script, str(save_path), str(manifest_path)], capture_output=True, text=True)
assert result.returncode != 0 and "manifest.json を読めない" in result.stderr, result.stderr
assert save_path.read_text() == intact
PY
