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

# PR #1256のCI実測を基に、PlayMode遷移を伴うClient fixtureを約3分ずつへ均等化する。
# Balance Client fixtures that transition PlayMode into roughly three-minute groups using PR #1256 CI timings.
client_play_1='Client\.Tests\.EditModeInPlayingTest\.MapObjects\.MapObjectNearestSearchTest|Client\.Tests\.EditModeInPlayingTest\.PlayerStartsOnBuiltTerrainTest|Client\.Tests\.EditModeInPlayingTest\.MachineModuleSlotUITest|Client\.Tests\.EditModeInPlayingTest\.MapObjects\.MapObjectRotationTest|Client\.Tests\.EditModeInPlayingTest\.DebugParametersIsolationAcrossDomainReloadTest|Client\.Tests\.EditModeInPlayingTest\.OsInputSpoofTest'
client_play_2='Client\.Tests\.EditModeInPlayingTest\.ElectricToGearModeSelectUITest|Client\.Tests\.EditModeInPlayingTest\.TerrainCacheFetchTest|Client\.Tests\.EditModeInPlayingTest\.MachineRecipeSelectionUITest|Client\.Tests\.EditModeInPlayingTest\.MapVeinOutcropAndRangeViewTest|Client\.Tests\.EditModeInPlayingTest\.Skit\.SkitWorldObjectRegistrationTest'
client_play_3='Client\.Tests\.EditModeInPlayingTest\.LocalPlayEmbeddedServerBootTest|Client\.Tests\.EditModeInPlayingTest\.BlockClickColliderTest|Client\.Tests\.EditModeInPlayingTest\.ChallengeListUITest|Client\.Tests\.EditModeInPlayingTest\.MachineRecipeSelectionGearUITest|Client\.Tests\.EditModeInPlayingTest\.EquipmentSelectionSynchronizationTest'
client_dedicated="${client_play_1}|${client_play_2}|${client_play_3}"

# MapGenerationの重量fixtureだけを専用化し、未列挙テストは必ずServer残余へ流す。
# Dedicate only heavy MapGeneration fixtures so every unlisted test always flows into the Server remainder.
server_map_1='Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.MultiTileGenerationTest|Tests\.UnitTest\.Game\.MapGeneration\.SpawnOffsetSceneSpaceTest|Tests\.UnitTest\.Game\.MapGeneration\.Provisioning\.GenerationMasterDriftResolverTest|Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.TileBoundarySeamTest'
server_map_2='Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.MultiTileMapObjectTransferTest|Tests\.UnitTest\.Game\.MapGeneration\.SpawnSearchDiagnosticsLogTest|Tests\.UnitTest\.Game\.MapGeneration\.Tiling\.TilePlacementWorldSpaceTest|Tests\.UnitTest\.Game\.MapGeneration\.Facade\.TerrainVisualPrebakeTest|Tests\.UnitTest\.Game\.MapGeneration\.MapGenerationPipelineTest|Tests\.UnitTest\.Game\.MapGeneration\.Visual\.Golden\.TerrainVisualGoldenTest'
server_map_3='Tests\.UnitTest\.Game\.MapGeneration\.Visual\.TileVisualBakerBoundaryTest|Tests\.UnitTest\.Game\.MapGeneration\.TerrainTransferMetaReaderTest|Tests\.UnitTest\.Game\.MapGeneration\.Placement\.ObjectScatterSpawnBandTest|Tests\.UnitTest\.Game\.MapGeneration\.Visual\.Placement\.PlacementLedgerTest|Tests\.UnitTest\.Game\.MapGeneration\.WorldProvisionerTest|Tests\.UnitTest\.Game\.MapGeneration\.Facade\.WorldTerrainSessionTest|Tests\.UnitTest\.Game\.MapGeneration\.TerrainChunkReaderTest'
server_dedicated="${server_map_1}|${server_map_2}|${server_map_3}"

# 専用fixtureはassemblyで絞り、残余は全assemblyを名前空間で排他的に分割して将来のtest assemblyも回収する。
# Scope dedicated fixtures by assembly, then partition all assemblies exclusively by namespace so future test assemblies are retained.
case "$1" in
  client-play-1) assembly_names='Client.Tests'; test_filter="^(${client_play_1})(\\.|$)"; needs_webui='true' ;;
  client-play-2) assembly_names='Client.Tests'; test_filter="^(${client_play_2})(\\.|$)"; needs_webui='true' ;;
  client-play-3) assembly_names='Client.Tests'; test_filter="^(${client_play_3})(\\.|$)"; needs_webui='true' ;;
  client-remainder) assembly_names=''; test_filter="^(?!(${client_dedicated})(\\.|$))Client\\."; needs_webui='true' ;;
  server-map-1) assembly_names='Server.Tests'; test_filter="^(${server_map_1})(\\.|$)"; needs_webui='false' ;;
  server-map-2) assembly_names='Server.Tests'; test_filter="^(${server_map_2})(\\.|$)"; needs_webui='false' ;;
  server-map-3) assembly_names='Server.Tests'; test_filter="^(${server_map_3})(\\.|$)"; needs_webui='false' ;;
  server-remainder) assembly_names=''; test_filter="!^(Client\\.|(${server_dedicated})(\\.|$))"; needs_webui='false' ;;
  *) echo "unknown Unity test shard: $1" >&2; exit 2 ;;
esac

# GitHub Actionsの単一行outputとして安全に受け渡す。
# Emit values as single-line GitHub Actions outputs.
printf 'assembly_names=%s\n' "$assembly_names" >> "$GITHUB_OUTPUT"
printf 'test_filter=%s\n' "$test_filter" >> "$GITHUB_OUTPUT"
printf 'needs_webui=%s\n' "$needs_webui" >> "$GITHUB_OUTPUT"
