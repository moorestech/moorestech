// 複数レシピの前後送りページャ（< i/n >）
// Pager for stepping through multiple recipes (< i/n >)
export default function RecipePager({
  index,
  count,
  setIndex,
}: {
  index: number;
  count: number;
  setIndex: (i: number) => void;
}) {
  if (count <= 1) return null;
  return (
    <div className="flex items-center gap-2 text-sm text-gray-300">
      <button onClick={() => setIndex((index + count - 1) % count)} className="bg-gray-700 hover:bg-gray-600 rounded px-2 py-0.5">
        &lt;
      </button>
      <span>
        {index + 1}/{count}
      </span>
      <button onClick={() => setIndex((index + 1) % count)} className="bg-gray-700 hover:bg-gray-600 rounded px-2 py-0.5">
        &gt;
      </button>
    </div>
  );
}
