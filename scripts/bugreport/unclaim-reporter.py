# 再現用の複製セーブで報告者を持ち主未定の候補へ戻す（ADR 0073）
# Return the reporter to an unclaimed candidate in the copied reproduction save (ADR 0073)
import json
import os
import sys
import tempfile


def note(message):
    sys.stderr.write("[unclaim-reporter] " + message + "\n")


def read_json(path, failure):
    # JSONとディスクは外部境界。読めない資料は理由と再現への影響を残す
    # JSON and disk are external boundaries; report unreadable evidence and its reproduction impact
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except Exception as error:
        note("%s（%s: %s）" % (failure, type(error).__name__, error))
        return None


def main(save_path, manifest_path):
    save = read_json(save_path, "save.json を読めないため報告者の付け替えをしない")
    if not isinstance(save, dict):
        note("save.json が辞書でないため報告者の付け替えをしない")
        return 1
    players = save.get("players")
    if players is None:
        note("版2のセーブは付け替えできない。ロード時変換の持ち物最大候補の選択に委ねる（報告者と一致する保証なし）")
        return 1
    if not isinstance(players, dict) or not isinstance(players.get("entries"), list):
        note("players 節が不正なため報告者の付け替えをしない")
        return 1
    entries = players["entries"]
    if any(not isinstance(entry, dict) or type(entry.get("playerId")) is not int
           or entry["playerId"] < 1 for entry in entries):
        note("players のプレイヤーIDが不正なため報告者の付け替えをしない")
        return 1

    # 報告者は manifest の身元で厳密に引く。裏付けの無い推測で他人の状態を持ち主未定へ戻さない
    # The reporter is identified by exactly matching a manifest identity; no unbacked guess sends another player's state back to unclaimed
    manifest = read_json(manifest_path, "manifest.json を読めない。報告者の身元なしとして扱う")
    manifest = manifest if isinstance(manifest, dict) else {}
    steam_id = manifest.get("steamId")
    identity = None
    if isinstance(steam_id, str) and steam_id:
        identity = "steam:" + steam_id
    else:
        device_identity = manifest.get("deviceIdentity")
        if isinstance(device_identity, str) and device_identity:
            identity = device_identity
    if identity is None:
        note("manifest に steamId も deviceIdentity も無いため報告者の付け替えをしない")
        return 1
    target = next((entry for entry in entries if entry.get("identity") == identity), None)
    if target is None:
        note("報告者の身元 %s に結びつくプレイヤーが無いため付け替えしない" % identity)
        return 1

    previous_identity = target["identity"]
    target["identity"] = None
    players["claimCandidatePlayerId"] = target["playerId"]
    temporary_path = None
    # ディスクは外部境界。置換前に書き終え、失敗時は元の複製セーブを保つ
    # Disk is an external boundary; finish writing before replacement to preserve the copied save on failure
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=os.path.dirname(save_path),
                                         prefix=".unclaim-reporter-", delete=False) as handle:
            temporary_path = handle.name
            json.dump(save, handle, ensure_ascii=False)
        os.replace(temporary_path, save_path)
    except OSError as error:
        note("save.json を更新できないため報告者の付け替えは未完了（%s）" % error)
        return 1
    finally:
        if temporary_path is not None and os.path.exists(temporary_path):
            try:
                os.unlink(temporary_path)
            except OSError as error:
                note("更新用一時ファイルを削除できない（%s: %s）" % (temporary_path, error))
    note("開発機が報告者として接続できるよう、プレイヤー%d（身元 %s）を持ち主未定の結びつけ候補へ戻す"
         % (target["playerId"], previous_identity))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
