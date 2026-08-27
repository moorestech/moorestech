#!/usr/bin/env bash
set -euo pipefail

# shard名とGitHub出力先を必須化し、無効設定で全件実行へ退化するのを防ぐ。
# Require the shard name and GitHub output path so invalid configuration cannot degrade into a full test run.
if [[ $# -ne 1 ]]; then
  echo "usage: $0 <shard-name>" >&2
  exit 2
fi
if [[ -z "${GITHUB_OUTPUT:-}" ]]; then
  echo "GITHUB_OUTPUT is required" >&2
  exit 2
fi

# project所有のtest asmdefをallowlistと照合し、新規assemblyを無音で検査対象外にしない。
# Compare project-owned test asmdefs with the allowlist so a new assembly cannot silently escape CI coverage.
client_test_assembly='Client.Tests'
server_test_assembly='Server.Tests'
addressables_test_assembly='Unity.Addressables.DocExampleCode.Editor.Tests'
known_project_test_assemblies=";${client_test_assembly};${server_test_assembly};"

# asmdefのreferencesはUnityが名前とGUIDのどちらでも書くため、TestRunnerの実GUIDでも照合する。
# Unity writes asmdef references either as names or GUIDs, so match TestRunner by its real GUID too.
unity_engine_test_runner_guid='GUID:27619889b8ba8c24980f49ee34dbb44a'
unity_editor_test_runner_guid='GUID:0acc523941302664db1f4e527237feb3'
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
project_test_assemblies=$(
  find "$repo_root/moorestech_client/Assets" "$repo_root/moorestech_server/Assets" -name '*.asmdef' -type f -print0 |
    while IFS= read -r -d '' asmdef; do
      jq -r --arg engineGuid "$unity_engine_test_runner_guid" --arg editorGuid "$unity_editor_test_runner_guid" \
        'select((([.references[]?] | any(. == "UnityEngine.TestRunner" or . == "UnityEditor.TestRunner" or . == $engineGuid or . == $editorGuid))) or (([.precompiledReferences[]?] | any(. == "nunit.framework.dll"))) or (([.optionalUnityReferences[]?] | any(. == "TestAssemblies")))) | .name' "$asmdef"
    done |
    sort -u
)
unexpected_test_assemblies=''
while IFS= read -r assembly_name; do
  if [[ -n "$assembly_name" && "$known_project_test_assemblies" != *";${assembly_name};"* ]]; then
    unexpected_test_assemblies+="${assembly_name}"$'\n'
  fi
done <<< "$project_test_assemblies"
if [[ -n "$unexpected_test_assemblies" ]]; then
  echo "new Unity test assemblies must be assigned to a remainder shard:" >&2
  printf '%s' "$unexpected_test_assemblies" >&2
  exit 2
fi

# PR #1256のCI実測を基に、PlayMode遷移を伴うClient fixtureを約3分ずつへ均等化する。
# Balance Client fixtures that transition PlayMode into roughly three-minute groups using PR #1256 CI timings.
client_play_1='Client\.Tests\.EditModeInPlayingTest\.MapObjects\.MapObjectNearestSearchTest|Client\.Tests\.EditModeInPlayingTest\.PlayerStartsOnBuiltTerrainTest|Client\.Tests\.EditModeInPlayingTest\.MachineModuleSlotUITest|Client\.Tests\.EditModeInPlayingTest\.MapObjects\.MapObjectRotationTest|Client\.Tests\.EditModeInPlayingTest\.DebugParametersIsolationAcrossDomainReloadTest|Client\.Tests\.EditModeInPlayingTest\.OsInputSpoofTest|Client\.Tests\.StartGameTest\.StartGameCheckTest'
client_play_2='Client\.Tests\.EditModeInPlayingTest\.ElectricToGearModeSelectUITest|Client\.Tests\.EditModeInPlayingTest\.TerrainCacheFetchTest|Client\.Tests\.EditModeInPlayingTest\.MachineRecipeSelectionUITest|Client\.Tests\.EditModeInPlayingTest\.MapVeinOutcropAndRangeViewTest|Client\.Tests\.EditModeInPlayingTest\.Skit\.SkitWorldObjectRegistrationTest'
# 未解決アドレス検証はAddressablesの初期化済み実行環境を要るのでPlayMode群へ同居させる。
# The unresolved-address test needs an initialized Addressables runtime, so it rides along with a PlayMode group.
client_play_3='Client\.Tests\.EditModeInPlayingTest\.LocalPlayEmbeddedServerBootTest|Client\.Tests\.EditModeInPlayingTest\.BlockClickColliderTest|Client\.Tests\.EditModeInPlayingTest\.ChallengeListUITest|Client\.Tests\.EditModeInPlayingTest\.MachineRecipeSelectionGearUITest|Client\.Tests\.EditModeInPlayingTest\.EquipmentSelectionSynchronizationTest|Client\.Tests\.UnitTest\.Terrain\.DetailPrototypeAssetResolverTest'

# 残余で不安定なnear-field起動を隔離し、単独実行不能なstart-gameはPlayMode群へ含める。
# Isolate near-field startup from the remainder while keeping start-game in a PlayMode group because it cannot run alone.
client_near_field_startup='Client\.Tests\.EditModeInPlayingTest\.MapObjects\.MapObjectNearFieldStartupTest'
client_dedicated="${client_play_1}|${client_play_2}|${client_play_3}|${client_near_field_startup}"

# MapGenerationの重量fixtureだけを専用化し、未列挙テストは必ずServer残余へ流す。
# Dedicate only heavy MapGeneration fixtures so every unlisted test always flows into the Server remainder.
server_map_1='Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.MultiTileGenerationTest|Tests\.UnitTest\.Game\.MapGeneration\.SpawnOffsetSceneSpaceTest|Tests\.UnitTest\.Game\.MapGeneration\.Provisioning\.GenerationMasterDriftResolverTest|Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.TileBoundarySeamTest'
server_map_2='Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.MultiTileMapObjectTransferTest|Tests\.UnitTest\.Game\.MapGeneration\.SpawnSearchDiagnosticsLogTest|Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.TilePlacementWorldSpaceTest|Tests\.UnitTest\.Game\.MapGeneration\.Facade\.TerrainVisualPrebakeTest|Tests\.UnitTest\.Game\.MapGeneration\.MapGenerationPipelineTest|Tests\.UnitTest\.Game\.MapGeneration\.Visual\.Golden\.TerrainVisualGoldenTest'
server_map_3='Tests\.UnitTest\.Game\.MapGeneration\.Visual\.TileVisualBakerBoundaryTest|Tests\.UnitTest\.Game\.MapGeneration\.TerrainTransferMetaReaderTest|Tests\.UnitTest\.Game\.MapGeneration\.Placement\.ObjectScatterSpawnBandTest|Tests\.UnitTest\.Game\.MapGeneration\.Visual\.Placement\.PlacementLedgerTest|Tests\.UnitTest\.Game\.MapGeneration\.WorldProvisionerTest|Tests\.UnitTest\.Game\.MapGeneration\.Facade\.WorldTerrainSessionTest|Tests\.UnitTest\.Game\.MapGeneration\.TerrainChunkReaderTest'
server_dedicated="${server_map_1}|${server_map_2}|${server_map_3}"
all_dedicated="${client_dedicated}|${server_dedicated}"

# 専用FQNはassembly横断で拾い、残余だけ既知assemblyへ絞って探索固定費を抑える。
# Match dedicated FQNs across assemblies, while scoping only remainders to known assemblies to reduce discovery overhead.
case "$1" in
  client-play-1) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${client_play_1})(\\.|$)"; needs_webui='true' ;;
  client-play-2) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${client_play_2})(\\.|$)"; needs_webui='true' ;;
  client-play-3) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${client_play_3})(\\.|$)"; needs_webui='true' ;;
  client-near-field-startup) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${client_near_field_startup})(\\.|$)"; needs_webui='true' ;;
  client-remainder) use_assembly_filter='true'; assembly_names="$client_test_assembly"; test_filter="!^(${all_dedicated})(\\.|$)"; needs_webui='true' ;;
  server-map-1) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${server_map_1})(\\.|$)"; needs_webui='false' ;;
  server-map-2) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${server_map_2})(\\.|$)"; needs_webui='false' ;;
  server-map-3) use_assembly_filter='false'; assembly_names='all'; test_filter="^(${server_map_3})(\\.|$)"; needs_webui='false' ;;
  server-remainder) use_assembly_filter='true'; assembly_names="${server_test_assembly};${addressables_test_assembly}"; test_filter="!^(${all_dedicated})(\\.|$)"; needs_webui='false' ;;
  *) echo "unknown Unity test shard: $1" >&2; exit 2 ;;
esac

# GitHub Actionsの単一行outputとして安全に受け渡す。
# Emit values as single-line GitHub Actions outputs.
printf 'use_assembly_filter=%s\n' "$use_assembly_filter" >> "$GITHUB_OUTPUT"
printf 'assembly_names=%s\n' "$assembly_names" >> "$GITHUB_OUTPUT"
printf 'test_filter=%s\n' "$test_filter" >> "$GITHUB_OUTPUT"
printf 'needs_webui=%s\n' "$needs_webui" >> "$GITHUB_OUTPUT"
