using Client.Game.InGame.UI.UIState.State.RemovePreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState.State;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     電線・チェーン共通の削除対象。1線＝1削除単位
    ///     Delete target shared by wires and chains; one line = one unit
    /// </summary>
    public class ConnectionLineDeleteTarget : MonoBehaviour, IDeleteTarget, IRemovePreviewable
    {
        public BlockInstanceId FromId { get; private set; }
        public BlockInstanceId ToId { get; private set; }
        public Guid ConnectToolGuid { get; private set; }
        public ConnectionLineKind Kind { get; private set; }

        private readonly RemovePreviewRequests _removePreviewRequests = new();
        private ConnectionLineRegistry _registry;
        private IConnectionLineEndpointQuery _endpoints;
        private IConnectionLineCommands _commands;
        private RendererMaterialReplacerController _materialReplacer;

        public void Initialize(BlockInstanceId fromId, BlockInstanceId toId, Guid connectToolGuid, ConnectionLineKind kind, ConnectionLineRegistry registry, IConnectionLineEndpointQuery endpoints, IConnectionLineCommands commands)
        {
            FromId = fromId;
            ToId = toId;
            ConnectToolGuid = connectToolGuid;
            Kind = kind;
            _registry = registry;
            _endpoints = endpoints;
            _commands = commands;
            _registry.Register(this);
        }

        // 当たり判定は線本体の子オブジェクトにあるため親を辿って本体を得る（線でなければnull）
        // Hit colliders live on child objects of the line, so climb to the parent for the line itself (null when not a line)
        public static ConnectionLineDeleteTarget FromCollider(Collider collider)
        {
            return collider.GetComponentInParent<ConnectionLineDeleteTarget>();
        }

        // 自分のホバー・選択は自分自身を要求者として赤を求める
        // Own hover/selection requests red with this component as the requester
        public void SetRemovePreviewing()
        {
            RequestRemovePreview(this);
        }

        public void ResetMaterial()
        {
            ReleaseRemovePreview(this);
        }

        public void RequestRemovePreview(object requester)
        {
            if (!_removePreviewRequests.Add(requester)) return;

            // レンダラーはSetLine後に揃うため、置換器は初回プレビュー時に作る
            // Renderers are complete only after SetLine, so build the replacer on the first preview
            _materialReplacer ??= new RendererMaterialReplacerController(gameObject);
            _materialReplacer.CopyAndSetMaterial(MaterialConst.GetPreviewPlaceBlockMaterial());
            _materialReplacer.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        // 最後の要求者が外れたときだけ元へ戻す（巻き込み表示の解除が他者の赤を消さない）
        // Reset only when the last requester leaves (a cascade release never clears someone else's red)
        public void ReleaseRemovePreview(object requester)
        {
            if (!_removePreviewRequests.Remove(requester)) return;
            _materialReplacer?.ResetMaterial();
        }

        // 両端ブロックの座標を解決する（切断送信とUndo記録が共有）
        // Resolve both endpoint block positions (shared by the disconnect send and the undo record)
        private bool TryResolveEndpointPositions(out Vector3Int fromPos, out Vector3Int toPos)
        {
            fromPos = default;
            toPos = default;
            if (!_endpoints.TryGetPosition(FromId, out fromPos) || !_endpoints.TryGetPosition(ToId, out toPos))
            {
                Debug.LogWarning($"[ConnectionLineDelete] endpoint block not found: from={FromId} to={ToId}");
                return false;
            }

            return true;
        }

        // 切断可否はサーバーが判定し拒否は通知で返るため、クライアントでは常に削除可とする
        // The server judges disconnects and returns refusals as notifications, so the client always allows it
        public bool IsRemovable(out LocalizationKey? deniedReason)
        {
            deniedReason = null;
            return true;
        }

        public void CollectRemovedObjects(RemovedObjectCollector collector)
        {
            // 端点ブロックが解決できない線は復元先を持てないため記録不能として数える
            // A line whose endpoints cannot be resolved has no restore target, so count it as unrecordable
            RemovedConnectionLine.Capture(this, collector);
        }

        public void Delete()
        {
            // 両端を解決して種類別の切断要求を送る
            // Resolve both ends and send the per-kind disconnect request
            if (!TryResolveEndpointPositions(out var fromPos, out var toPos)) return;

            _commands.SendDisconnect(fromPos, toPos);
        }

        public bool TryGetRestoreData(out Vector3Int fromPos, out Vector3Int toPos)
        {
            return TryResolveEndpointPositions(out fromPos, out toPos);
        }

        public IConnectionLineCommands GetLineCommands()
        {
            return _commands;
        }

        // 線は1本ごとに1つのGameObjectなので自身を論理キーにする
        // Each line owns one GameObject, so the component itself is the logical key
        public object GetDeleteTargetKey()
        {
            return this;
        }

        public string GetDestructionCategory()
        {
            return BlockMasterElementExtension.ConnectionLineDestructionCategory;
        }

        private void OnDestroy()
        {
            _registry?.Unregister(this);
            _materialReplacer?.DestroyMaterial();
        }
    }
}
