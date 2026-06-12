import { dispatchAction } from "../bridge/actions";
import { showToast } from "../bridge/toastBus";

// debug.echo を発行して双方向APIの疎通を確認する開発用ボタン
// Dev button that sends debug.echo to verify the bidirectional API
export default function DebugActionButton() {
  const onClick = async () => {
    const ok = await dispatchAction("debug.echo", { hello: "world" });
    if (ok) showToast("debug.echo ok");
  };

  return (
    <button onClick={onClick} className="bg-gray-700 hover:bg-gray-600 text-sm rounded px-3 py-1">
      Ping Action
    </button>
  );
}
