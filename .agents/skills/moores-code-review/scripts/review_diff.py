#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# =====================================================================
"""レビュー用 patch に入れないパスの正本と、それを付けて git diff を取る入口。

moores-code-review（セッションの patch.diff）と pr-independent-review（make_patch.py）の両方がここを使う。
外すのは「レビューで裁かない物」だけ: 手を入れない外部物（NuGet の同梱パッケージ・ロックファイル）、
生成物（DO NOT EDIT の自動生成コード）、バイナリ（--text 付きの diff ではバイト列がそのまま patch へ流れる）、
Unity のシリアライズ資産（YAML。コードレビューの対象外）、使い捨てのプレイテストシナリオ。
チームが手を入れる外部アセットのコード（Assets/Dependencies の .cs 等）は外さない。

Single source of truth for paths kept out of review patches, plus an entry point that runs git diff with them.
Both moores-code-review (the session patch.diff) and pr-independent-review (make_patch.py) use it.
Only things reviews never judge are excluded: untouched third-party code (vendored NuGet packages, lockfiles),
generated code (DO NOT EDIT), binaries (a --text diff would dump their bytes into the patch),
Unity serialized assets (YAML, outside code review), and throwaway playtest scenarios.
Third-party asset code the team does edit (e.g. .cs under Assets/Dependencies) stays in.

Usage: python3 review_diff.py [git diff の引数...]   例: review_diff.py <base>^..<last> / review_diff.py --cached
"""
from __future__ import annotations

import subprocess
import sys

# 手を入れない外部物と生成物
# Untouched third-party code and generated files
VENDORED_AND_GENERATED = [
    ":(exclude,glob)**/Assets/Packages/**",
    ":(exclude,glob)**/packages-lock.json",
    ":(exclude,glob)**/pnpm-lock.yaml",
    ":(exclude,glob)**/package-lock.json",
    ":(exclude,glob)**/yarn.lock",
    ":(exclude,glob)**/i18n/generated/**",
    # 使い捨ての操作台本で、プロダクトコードの規約で裁く対象ではない（ユーザー裁定 2026-08-16 / PR#1137-F12）
    # Throwaway scenario scripts, not judged by product-code rules (user ruling 2026-08-16 / PR#1137-F12)
    ":(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs",
]

# Unity のシリアライズ資産（YAML）
# Unity serialized assets (YAML)
UNITY_SERIALIZED_EXTENSIONS = [
    "meta", "prefab", "asset", "unity", "controller", "overrideController", "mat", "anim", "mask",
    "playable", "signal", "lighting", "terrainlayer", "spriteatlas", "spriteatlasv2", "renderTexture",
    "mixer", "preset", "physicMaterial", "physicsMaterial2D", "shadervariants", "cubemap", "flare",
    "guiskin", "brush", "giparams",
]

# バイナリ（画像・動画・音声・フォント・3Dモデル・実行物・アーカイブ）
# Binaries (images, video, audio, fonts, 3D models, executables, archives)
BINARY_EXTENSIONS = [
    "png", "jpg", "jpeg", "gif", "webp", "bmp", "tga", "tif", "tiff", "psd", "exr", "hdr", "ico", "icns",
    "mp4", "mov", "webm", "wav", "mp3", "ogg", "ttf", "otf", "fbx", "obj", "blend",
    "dll", "exe", "so", "dylib", "pdb", "a", "lib", "zip", "unitypackage", "bytes",
]
BINARY_DIRECTORIES = [":(exclude,glob)**/*.app/**"]


def exclude_pathspecs() -> list[str]:
    extensions = UNITY_SERIALIZED_EXTENSIONS + BINARY_EXTENSIONS
    # 拡張子は大文字の資産（.PNG・.FBX 等）もあるため大小を区別しない
    # Extensions match case-insensitively since some assets use upper case (.PNG, .FBX, ...)
    return VENDORED_AND_GENERATED + [f":(exclude,glob,icase)**/*.{ext}" for ext in extensions] + BINARY_DIRECTORIES


def main() -> int:
    # 外部境界（git プロセス起動）。stdout をそのまま patch として流す
    # External boundary: spawning git; its stdout is streamed out as the patch
    diff = subprocess.run(["git", "diff", *sys.argv[1:], "--", ".", *exclude_pathspecs()],
                          capture_output=True, text=True)
    if diff.returncode != 0:
        sys.stderr.write(f"review_diff: git diff 失敗: {diff.stderr.strip()}\n")
        return diff.returncode
    sys.stdout.write(diff.stdout)
    return 0


if __name__ == "__main__":
    sys.exit(main())
