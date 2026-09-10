#!/usr/bin/env bash
set -euo pipefail

# shard集合の正本をこのスクリプト1本に置き、matrixもfilterも同じ配列から導く。
# This script is the single source of the shard set; both the matrix and the filters derive from the same arrays.
dedicated_shards=(
  client-play-1
  client-play-2
  client-play-3
  client-near-field-startup
  server-map-1
  server-map-2
  server-map-3
)
remainder_shards=(
  client-remainder
  server-remainder
)

# shard名からテスト側のCategory名を導く。割当の正本はテストクラスの[Category]属性で、改名・移動に追従する。
# Derive the test-side category name from the shard name; the assignment itself lives in each test class's [Category] attribute and follows renames and moves.
shard_category_name() {
  local shard_name=$1
  local category_name='CiShard'
  local segment
  for segment in ${shard_name//-/ }; do
    category_name+="$(printf '%s' "${segment:0:1}" | tr '[:lower:]' '[:upper:]')${segment:1}"
  done
  printf '%s' "$category_name"
}

# 走査対象のUnityプロジェクトと、そのプロジェクトが持つ既知のtest assembly。remainderの-assemblyNamesもここから導く。
# The Unity projects to scan and the known test assemblies each one owns; the remainder's -assemblyNames derives from here too.
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
client_project="$repo_root/moorestech_client"
server_project="$repo_root/moorestech_server"
client_test_assemblies='Client.Tests'
# Addressablesのテストはパッケージ同梱でプロジェクト内にasmdefを持たないが、実行対象なのでallowlistへ明記する。
# The Addressables tests ship inside a package and own no asmdef in the project, yet they do run, so the allowlist states them explicitly.
server_test_assemblies='Server.Tests;Unity.Addressables.DocExampleCode.Editor.Tests'
known_project_test_assemblies=";${client_test_assemblies};${server_test_assemblies};"

# Assets配下だけを見ると、Packages/LocalPackagesのtest assemblyがどのshardでも実行されないまま全greenになる。
# Scanning only Assets would let a test assembly under Packages or LocalPackages go unexecuted in every shard while CI stays green.
list_project_test_assemblies() {
  local scan_roots=()
  local project
  local scan_directory
  for project in "$client_project" "$server_project"; do
    for scan_directory in Assets Packages LocalPackages; do
      [[ -d "$project/$scan_directory" ]] && scan_roots+=("$project/$scan_directory")
    done
  done

  # asmdefのreferencesはUnityが名前とGUIDのどちらでも書くため、TestRunnerの実GUIDでも照合する。
  # Unity writes asmdef references either as names or GUIDs, so match TestRunner by its real GUID too.
  local unity_engine_test_runner_guid='GUID:27619889b8ba8c24980f49ee34dbb44a'
  local unity_editor_test_runner_guid='GUID:0acc523941302664db1f4e527237feb3'
  find "${scan_roots[@]}" -name '*.asmdef' -type f -print0 |
    while IFS= read -r -d '' asmdef; do
      jq -r --arg engineGuid "$unity_engine_test_runner_guid" --arg editorGuid "$unity_editor_test_runner_guid" \
        'select((([.references[]?] | any(. == "UnityEngine.TestRunner" or . == "UnityEditor.TestRunner" or . == $engineGuid or . == $editorGuid))) or (([.precompiledReferences[]?] | any(. == "nunit.framework.dll"))) or (([.optionalUnityReferences[]?] | any(. == "TestAssemblies")))) | .name' "$asmdef"
    done |
    sort -u
}

# manifestのtestablesはパッケージ内テストを実行対象に加える宣言。allowlistに現れないままだとどのshardでも走らない。
# A manifest testable declares that a package's tests run; one absent from the allowlist would execute in no shard at all.
list_manifest_testables() {
  local project
  for project in "$client_project" "$server_project"; do
    [[ -f "$project/Packages/manifest.json" ]] && jq -r '.testables[]?' "$project/Packages/manifest.json"
  done | sort -u
}

assert_every_test_assembly_is_assigned() {
  local unexpected_test_assemblies=''
  local assembly_name
  while IFS= read -r assembly_name; do
    if [[ -n "$assembly_name" && "$known_project_test_assemblies" != *";${assembly_name};"* ]]; then
      unexpected_test_assemblies+="${assembly_name}"$'\n'
    fi
  done <<< "$(list_project_test_assemblies)"

  local testable_package
  while IFS= read -r testable_package; do
    [[ -n "$testable_package" ]] && unexpected_test_assemblies+="testable package: ${testable_package}"$'\n'
  done <<< "$(list_manifest_testables)"

  if [[ -n "$unexpected_test_assemblies" ]]; then
    echo "new Unity test assemblies must be assigned to a remainder shard:" >&2
    printf '%s' "$unexpected_test_assemblies" >&2
    exit 2
  fi
}

# matrixはこの一覧から生成する。shardを消し忘れて該当fixtureがどこでも走らない状態を作らない。
# The matrix is generated from this list, so a forgotten shard cannot leave its fixtures running nowhere.
if [[ ${1:-} == '--list-shards' ]]; then
  assert_every_test_assembly_is_assigned
  printf '%s\n' "${dedicated_shards[@]}" "${remainder_shards[@]}" | jq -R . | jq -sc .
  exit 0
fi

# shard名とGitHub出力先を必須化し、無効設定で全件実行へ退化するのを防ぐ。
# Require the shard name and GitHub output path so invalid configuration cannot degrade into a full test run.
if [[ $# -ne 1 ]]; then
  echo "usage: $0 <shard-name>|--list-shards" >&2
  exit 2
fi
if [[ -z "${GITHUB_OUTPUT:-}" ]]; then
  echo "GITHUB_OUTPUT is required" >&2
  exit 2
fi

assert_every_test_assembly_is_assigned

# 残余は専用shardの全Categoryを除外して受ける。除外集合も配列から導くので手書きの取りこぼしが起きない。
# A remainder excludes every dedicated category, and that exclusion set derives from the same array, so nothing can be missed by hand.
excluded_dedicated_categories=''
for dedicated_shard in "${dedicated_shards[@]}"; do
  excluded_dedicated_categories+=";!$(shard_category_name "$dedicated_shard")"
done

shard_name=$1
case "$shard_name" in
  client-remainder)
    use_assembly_filter='true'
    assembly_names="$client_test_assemblies"
    test_category="!IgnoreCI${excluded_dedicated_categories}"
    needs_webui='true'
    ;;
  server-remainder)
    use_assembly_filter='true'
    assembly_names="$server_test_assemblies"
    test_category="!IgnoreCI${excluded_dedicated_categories}"
    needs_webui='false'
    ;;
  *)
    # 専用shardはCategoryだけで選ぶ。assembly横断の重量fixtureを1箇所に集められる
    # A dedicated shard selects purely by category, which lets heavy fixtures from different assemblies share one shard
    is_dedicated_shard='false'
    for dedicated_shard in "${dedicated_shards[@]}"; do
      [[ "$dedicated_shard" == "$shard_name" ]] && is_dedicated_shard='true'
    done
    if [[ "$is_dedicated_shard" != 'true' ]]; then
      echo "unknown Unity test shard: $shard_name" >&2
      exit 2
    fi
    use_assembly_filter='false'
    assembly_names='all'
    test_category="!IgnoreCI;$(shard_category_name "$shard_name")"
    if [[ "$shard_name" == client-* ]]; then
      needs_webui='true'
    else
      needs_webui='false'
    fi
    ;;
esac

# GitHub Actionsの単一行outputとして安全に受け渡す。
# Emit values as single-line GitHub Actions outputs.
printf 'use_assembly_filter=%s\n' "$use_assembly_filter" >> "$GITHUB_OUTPUT"
printf 'assembly_names=%s\n' "$assembly_names" >> "$GITHUB_OUTPUT"
printf 'test_category=%s\n' "$test_category" >> "$GITHUB_OUTPUT"
printf 'needs_webui=%s\n' "$needs_webui" >> "$GITHUB_OUTPUT"
