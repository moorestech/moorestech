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


def item_count(save, player_id):
    inventories = save.get("playerInventory", [])
    if not isinstance(inventories, list):
        raise ValueError("playerInventory が配列でない")
    inventory = next((entry for entry in inventories
                      if isinstance(entry, dict) and entry.get("PlayerId") == player_id), {})
    stacks = []
    for key in ("MainInventoryItems", "EquipmentInventoryItems"):
        items = inventory.get(key) or []
        if not isinstance(items, list):
            raise ValueError("%s が配列でない" % key)
        stacks.extend(items)
    grab = inventory.get("GrabInventoryItems")
    if isinstance(grab, dict):
        stacks.append(grab)
    # メイン・装備・つかみ中を合算し、壊れた個数から候補を推測しない
    # Sum main, equipment and held stacks without guessing a candidate from corrupt counts
    total = 0
    for stack in stacks:
        if not isinstance(stack, dict):
            continue
        count = stack.get("count", 0)
        if type(count) is not int or count < 0:
            raise ValueError("プレイヤー%dのアイテム個数が不正" % player_id)
        total += count
    return total


def main(save_path, manifest_path):
    save = read_json(save_path, "save.json を読めないため報告者の付け替えをしない")
    if not isinstance(save, dict):
        note("save.json が辞書でないため報告者の付け替えをしない")
        return
    players = save.get("players")
    if players is None:
        note("save.json に players 節が無い。ロード時の変換が候補を選ぶので付け替えは不要")
        return
    if not isinstance(players, dict) or not isinstance(players.get("entries"), list):
        note("players 節が不正なため報告者の付け替えをしない")
        return
    entries = players["entries"]
    if any(not isinstance(entry, dict) or type(entry.get("playerId")) is not int
           or entry["playerId"] < 1 for entry in entries):
        note("players のプレイヤーIDが不正なため報告者の付け替えをしない")
        return

    # manifest の身元が一致するときは持ち物数より優先する
    # Prefer the manifest identity over inventory size when it matches
    manifest = read_json(manifest_path, "manifest.json を読めない。報告者の身元なしとして扱う")
    steam_id = manifest.get("steamId") if isinstance(manifest, dict) else None
    target = None
    if isinstance(steam_id, str) and steam_id:
        identity = "steam:" + steam_id
        target = next((entry for entry in entries if entry.get("identity") == identity), None)
        if target is None:
            note("報告者の身元 %s に結びつくプレイヤーが無いため付け替えしない" % identity)
            return
    else:
        note("manifest に steamId が無い。端末身元の持ち物総数で選ぶ")

    # ADRの対象は端末身元のみ。同数ならIDで決めて再現を安定させる
    # ADR fallback covers device identities only; break inventory ties by ID for stable reproduction
    if target is None:
        bound = [entry for entry in entries if isinstance(entry.get("identity"), str)
                 and entry["identity"].startswith("device:")]
        if not bound:
            note("端末身元に結びついたプレイヤーが居ないため付け替えしない")
            return
        # 外部JSONの個数を検証し、壊れた入力では候補変更を保留する
        # Validate external JSON counts and withhold candidate changes for corrupt input
        try:
            target = min(bound, key=lambda entry: (-item_count(save, entry["playerId"]), entry["playerId"]))
        except ValueError as error:
            note("持ち物総数を読めないため報告者の付け替えをしない（%s）" % error)
            return

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
        return
    finally:
        if temporary_path is not None and os.path.exists(temporary_path):
            try:
                os.unlink(temporary_path)
            except OSError as error:
                note("更新用一時ファイルを削除できない（%s: %s）" % (temporary_path, error))
    note("開発機が報告者として接続できるよう、プレイヤー%d（身元 %s）を持ち主未定の結びつけ候補へ戻す"
         % (target["playerId"], previous_identity))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
