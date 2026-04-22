using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using UniRx;
using Random = System.Random;

namespace Game.Gear.Common
{
    public class GearNetworkDatastore
    {
        // TODO これってなんでstaticにしたんだっけ？こういうのは全般的にサービスロケーターにしたほうが良いような気がしてきた
        private static GearNetworkDatastore _instance;
        
        private readonly Dictionary<BlockInstanceId, GearNetwork> _blockEntityToGearNetwork; // key ブロックのEntityId value そのブロックが所属するNW
        private readonly Dictionary<GearNetworkId, GearNetwork> _gearNetworks = new();
        private readonly Random _random = new(215180);
        
        public GearNetworkDatastore()
        {
            _instance = this;
            _blockEntityToGearNetwork = new Dictionary<BlockInstanceId, GearNetwork>();
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        public IReadOnlyDictionary<GearNetworkId, GearNetwork> GearNetworks => _gearNetworks;
        
        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance.AddGearInternal(gear);
        }
        
        private void AddGearInternal(IGearEnergyTransformer gear)
        {
            var connectedNetworkIds = new HashSet<GearNetworkId>();
            foreach (var connectedGear in gear.GetGearConnects())
                //新しく設置された歯車に接続している歯車は、すべて既存のNWに接続している前提
                if (_blockEntityToGearNetwork.ContainsKey(connectedGear.Transformer.BlockInstanceId))
                {
                    var networkId = _blockEntityToGearNetwork[connectedGear.Transformer.BlockInstanceId].NetworkId;
                    connectedNetworkIds.Add(networkId);
                }
            
            //接続しているNWが1つもない場合は新規NWを作成
            switch (connectedNetworkIds.Count)
            {
                case 0:
                    CreateNetwork();
                    break;
                case 1:
                    ConnectNetwork();
                    break;
                default:
                    MergeNetworks();
                    break;
            }
            
            #region Internal
            
            void CreateNetwork()
            {
                var networkId = GearNetworkId.CreateNetworkId();
                var network = new GearNetwork(networkId);
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
                _gearNetworks.Add(networkId, network);
            }
            
            void ConnectNetwork()
            {
                var networkId = connectedNetworkIds.First();
                var network = _gearNetworks[networkId];
                network.AddGear(gear);
                _blockEntityToGearNetwork.Add(gear.BlockInstanceId, network);
            }
            
            void MergeNetworks()
            {
                // マージのために歯車を取得
                var transformers = new List<IGearEnergyTransformer>();
                var generators = new List<IGearGenerator>();
                
                foreach (var networkId in connectedNetworkIds.ToList())
                {
                    var network = _gearNetworks[networkId];
                    transformers.AddRange(network.GearTransformers);
                    generators.AddRange(network.GearGenerators);
                    _gearNetworks.Remove(networkId);
                }
                
                var newNetworkId = GearNetworkId.CreateNetworkId();
                var newNetwork = new GearNetwork(newNetworkId);
                
                foreach (var transformer in transformers) newNetwork.AddGear(transformer);
                foreach (var generator in generators) newNetwork.AddGear(generator);
                
                transformers.Add(gear);
                newNetwork.AddGear(gear);
                _blockEntityToGearNetwork[gear.BlockInstanceId] = newNetwork;
                
                // マージした旧NWに属していた歯車のNW参照を新NWに張り替える
                // Re-point every gear that belonged to a merged old network to the new network
                foreach (var t in transformers) _blockEntityToGearNetwork[t.BlockInstanceId] = newNetwork;
                foreach (var g in generators) _blockEntityToGearNetwork[g.BlockInstanceId] = newNetwork;
                
                _gearNetworks.Add(newNetworkId, newNetwork);
                foreach (var removeNetworkId in connectedNetworkIds) _gearNetworks.Remove(removeNetworkId);
            }
            
            #endregion
        }
        
        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            _instance.RemoveGearInternal(gear);
        }

        private void RemoveGearInternal(IGearEnergyTransformer gear)
        {
            // 所属ネットワークを引き、自身を除去
            // Look up owning network and remove the gear itself
            if (!_blockEntityToGearNetwork.TryGetValue(gear.BlockInstanceId, out var network)) return;
            _blockEntityToGearNetwork.Remove(gear.BlockInstanceId);
            network.RemoveGear(gear);

            // 残存ギア集合の総数。削除対象は既に除外済み
            // Total count of remaining gears; the removed one is already excluded
            var totalCount = network.GearTransformers.Count + network.GearGenerators.Count;

            if (totalCount == 0)
            {
                _gearNetworks.Remove(network.NetworkId);
                return;
            }

            // 高速パス: 削除ギアの次数が1以下ならグラフ理論上cut vertexになり得ないので、残存網は絶対に分裂しない。BFSごと省略できる
            // Fast path: if the removed gear has degree ≤ 1 it can never be a cut vertex, so the surviving network is guaranteed to stay connected; skip the whole BFS
            if (gear.GetGearConnects().Count <= 1) return;

            // 残存ギアの配列と id→index マップを1パスで構築する。後続BFSは配列への null 書き込みを visited マーク代わりに使う
            // Build the remaining gear array and the id→index map in a single pass; BFS below uses null-assignment into this array as its visited marker
            var remaining = new IGearEnergyTransformer[totalCount];
            var idToIdx = new Dictionary<BlockInstanceId, int>(totalCount);
            var fillIndex = 0;
            foreach (var g in network.GearTransformers)
            {
                remaining[fillIndex] = g;
                idToIdx[g.BlockInstanceId] = fillIndex;
                fillIndex++;
            }
            foreach (var g in network.GearGenerators)
            {
                remaining[fillIndex] = g;
                idToIdx[g.BlockInstanceId] = fillIndex;
                fillIndex++;
            }

            var components = FindComponents(remaining, idToIdx);

            // 分断なし → 既存ネットワークをそのまま維持（mapping 更新も不要）
            // No split: keep the existing network as-is; no mapping updates needed
            if (components.Count == 1) return;

            // 複数成分へ分断 → 既存ネットを破棄し、成分ごとに新ネットワークを生成
            // Split into multiple components: discard the old network and create a new network per component
            _gearNetworks.Remove(network.NetworkId);
            foreach (var component in components)
            {
                var newNetworkId = GearNetworkId.CreateNetworkId();
                var newNetwork = new GearNetwork(newNetworkId);
                foreach (var g in component)
                {
                    newNetwork.AddGear(g);
                    _blockEntityToGearNetwork[g.BlockInstanceId] = newNetwork;
                }
                _gearNetworks.Add(newNetworkId, newNetwork);
            }

            #region Internal

            static List<List<IGearEnergyTransformer>> FindComponents(IGearEnergyTransformer[] remaining, Dictionary<BlockInstanceId, int> idToIdx)
            {
                // 発見した連結成分を格納するリストと、BFSで使い回すキューを用意する。visited は remaining[idx] の null 化で表現するため別集合は持たない
                // Prepare the result list of components and a reusable queue; visited state is encoded by null-ing out remaining[idx], so no separate set is needed
                var components = new List<List<IGearEnergyTransformer>>();
                var queue = new Queue<IGearEnergyTransformer>();

                // 全スロットを昇順に走査し、まだ null 化されていないギアを新しい連結成分の起点として採用する
                // Walk every slot in ascending order; any gear not yet nulled out becomes the seed of a new connected component
                for (var i = 0; i < remaining.Length; i++)
                {
                    // null 化済み = 既に前の成分に回収済みなのでスキップ。配列の連続読み込みなのでキャッシュヒット率が高い
                    // A null slot means the gear was already consumed by an earlier component; array reads are contiguous and cache-friendly
                    var start = remaining[i];
                    if (start == null) continue;

                    // 起点を成分に加える前に先に null 化し、queue 経由で重複登録されるのを防ぐ
                    // Null out the seed before enqueueing so it cannot be queued twice via some cycle
                    remaining[i] = null;
                    var component = new List<IGearEnergyTransformer>();
                    queue.Clear();
                    queue.Enqueue(start);

                    // キューが空になるまでBFSを続け、起点から到達可能なギアを全てこの成分に集める
                    // Keep BFS running until the queue drains, collecting every gear reachable from the seed into this component
                    while (queue.Count > 0)
                    {
                        // 現在処理中のギアをキューから取り出し、成分メンバーとして確定する
                        // Pop the current gear from the queue and commit it as a member of the component
                        var current = queue.Dequeue();
                        component.Add(current);

                        // 現在ギアの全接続先を辿り、同じ成分に属するかを1件ずつ判定する
                        // Walk every connection of the current gear and classify each neighbor individually
                        foreach (var connect in current.GetGearConnects())
                        {
                            // idToIdx に無い = この残存ネットワークの外（削除ギア・別ネット・破棄済み）なので辺ごと遮断する
                            // Missing from idToIdx means the neighbor lies outside the surviving set (removed gear, another network, or stale); cut such edges entirely
                            if (!idToIdx.TryGetValue(connect.Transformer.BlockInstanceId, out var idx)) continue;
                            // remaining[idx] が null なら既に他経路から訪問済み。null で無ければここで null 化と同時にキューへ積む
                            // A null slot means already visited via another path; otherwise null it out and enqueue atomically
                            if (remaining[idx] == null) continue;
                            remaining[idx] = null;
                            queue.Enqueue(connect.Transformer);
                        }
                    }

                    // 起点から到達できた全ギアで1つの連結成分が確定したので結果に追加する
                    // All gears reachable from this seed form one finalized connected component, so record it
                    components.Add(component);
                }

                return components;
            }

            #endregion
        }
        
        private void Update()
        {
            foreach (var gearNetwork in _gearNetworks.Values) // TODO パフォーマンスがやばくなったらやめる
                gearNetwork.ManualUpdate();
        }
        
        public static GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            return _instance._blockEntityToGearNetwork[blockInstanceId];
        }

        // 未登録IDに対して例外を投げずに失敗を返す。プロトコル呼び出し側は存在しないブロックIDを送ってくる可能性があるため
        // Return a failure instead of throwing when the id is not registered; callers such as protocol handlers may receive ids for blocks that no longer exist
        public static bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            return _instance._blockEntityToGearNetwork.TryGetValue(blockInstanceId, out network);
        }
    }
}