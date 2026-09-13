#!/usr/bin/env python3
"""ブロックstateの文字列をオブジェクトへ展開し currentTick / randomState を付与する。

使い方: python3 migrate_block_state_objects.py <save.json> --seed <world.jsonのseed>
元ファイルは <save.json>.pre-state-objects.bak として残す。
"""
import argparse
import json
import shutil
import sys

MASK = (1 << 64) - 1


def splitmix64(x):
    # C# の GameRandom.Reseed と同じ手順（変更したら両方を直す）
    x = (x + 0x9E3779B97F4A7C15) & MASK
    z = x
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
    return x, z ^ (z >> 31)


def random_state_from_seed(seed):
    x = seed & MASK
    state = []
    for _ in range(4):
        x, word = splitmix64(x)
        state.append(word)
    return state


def migrate(save, seed):
    converted = 0
    for block in save["world"]:
        state = block.get("state") or {}
        for key, value in list(state.items()):
            if isinstance(value, str):
                parsed = json.loads(value)
                # 二重エンコードされた値は1回のloadsでまだ文字列のまま。移行済みに見えてロード時に例外で起動不能になる
                # A double-encoded value is still a string after one loads; it would look migrated yet break the load at boot
                if isinstance(parsed, str):
                    raise AssertionError(
                        f"state['{key}'] が二重エンコードされています。1回の展開では文字列のままです: {value[:80]}"
                    )
                state[key] = parsed
                converted += 1
        block["state"] = state
    if "currentTick" not in save:
        save["currentTick"] = 0
    if "randomState" not in save:
        save["randomState"] = random_state_from_seed(seed)
    return converted


# C# の GameRandomTest と同じ既知ベクトル。どちらかの定数を変えたらここで落ちる
# The same known vector as the C# GameRandomTest; changing a constant on either side fails here
KNOWN_SEED = 12345
KNOWN_STATE = [
    2454886589211414944,
    3778200017661327597,
    2205171434679333405,
    3248800117070709450,
]


def self_test():
    actual = random_state_from_seed(KNOWN_SEED)
    if actual != KNOWN_STATE:
        raise AssertionError(
            f"randomState が既知ベクトルと違う seed={KNOWN_SEED} expected={KNOWN_STATE} actual={actual}"
        )
    return 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("save_path", nargs="?")
    parser.add_argument("--seed", type=int)
    parser.add_argument("--self-test", action="store_true", help="既知ベクトルで splitmix64 を検査して終了する")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("self-test ok")
        return 0
    if args.save_path is None or args.seed is None:
        parser.error("save_path と --seed は必須です（--self-test のときを除く）")

    with open(args.save_path, encoding="utf-8") as f:
        save = json.load(f)
    backup = args.save_path + ".pre-state-objects.bak"
    shutil.copyfile(args.save_path, backup)
    converted = migrate(save, args.seed)
    with open(args.save_path, "w", encoding="utf-8") as f:
        json.dump(save, f, ensure_ascii=False)
    print(f"converted state values: {converted}, backup: {backup}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
