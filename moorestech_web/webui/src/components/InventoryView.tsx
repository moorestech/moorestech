import { useTopic } from "../bridge/useTopic";

type InventoryData = {
  slots: Array<{ itemId: number; count: number }>;
};

// ローカルプレイヤーのインベントリを WS 購読して表示
// Subscribe to the local player's inventory over WS and render it

// TODO Web側の設計は基本的に「データ層」「webscokect層」を分離して、描画側はwebscokectの存在を知らないようにしたい。とりあえず今はこれで妥協

export default function InventoryView() {
  const inventory = useTopic<InventoryData>("local_player.inventory");

  if (!inventory) {
    return <div className="text-sm text-gray-400">connecting...</div>;
  }

  return (
    <div>
      <h2 className="text-lg font-semibold mb-2">Inventory</h2>
      <div className="grid grid-cols-9 gap-1">
        {inventory.slots.map((s, i) => (
          <div
            key={i}
            className="border border-gray-700 rounded p-2 min-h-[48px] text-xs flex flex-col justify-between bg-gray-900"
          >
            <div className="text-gray-400">#{i}</div>
            {s.count > 0 ? (
              <div>
                <div className="text-white">id:{s.itemId}</div>
                <div className="text-green-400">×{s.count}</div>
              </div>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  );
}
