using System.Collections.Generic;
using Client.Common;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject
{
    /// <summary>
    ///     設置照準だけが当たる薄い面6枚。占有マスの直方体の各面に置く
    ///     Six thin faces hit only by the placement aim, one on each side of the occupied-cell box
    /// </summary>
    public sealed class BlockPlacementHitFaces : MonoBehaviour, IPreviewOnlyObject
    {
        // 旧プレハブの面と同じ厚み。内側から撃っても奥の面に当たる
        // Same thickness as the old prefab faces, so a ray from inside still hits the far face
        private const float FaceThickness = 0.14f;

        private readonly Dictionary<Collider, PreviewSurfaceType> _surfaceTypeByCollider = new();

        public static BlockPlacementHitFaces Create(Transform blockRoot, Vector3Int blockSize, BlockDirection blockDirection)
        {
            var hitFacesObject = new GameObject("BlockPlacementHitFaces") { layer = LayerConst.BlockBoundingBoxLayer };
            hitFacesObject.transform.SetParent(blockRoot, false);
            var hitFaces = hitFacesObject.AddComponent<BlockPlacementHitFaces>();

            // 面は旧プレハブと同じくブロックローカルの占有範囲に沿わせる
            // Faces follow the block-local footprint, matching the old prefab
            var size = (Vector3)blockSize;
            hitFaces.AddFace(new Vector3(size.x / 2, size.y / 2, 0), new Vector3(size.x, size.y, FaceThickness), new Vector3Int(0, 0, -1), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, size.y / 2, size.z), new Vector3(size.x, size.y, FaceThickness), new Vector3Int(0, 0, 1), blockDirection);
            hitFaces.AddFace(new Vector3(0, size.y / 2, size.z / 2), new Vector3(FaceThickness, size.y, size.z), new Vector3Int(-1, 0, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x, size.y / 2, size.z / 2), new Vector3(FaceThickness, size.y, size.z), new Vector3Int(1, 0, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, 0, size.z / 2), new Vector3(size.x, FaceThickness, size.z), new Vector3Int(0, -1, 0), blockDirection);
            hitFaces.AddFace(new Vector3(size.x / 2, size.y, size.z / 2), new Vector3(size.x, FaceThickness, size.z), new Vector3Int(0, 1, 0), blockDirection);
            return hitFaces;
        }

        public PreviewSurfaceType GetSurfaceType(Collider collider)
        {
            return _surfaceTypeByCollider[collider];
        }

        public void Initialize(BlockId blockId)
        {
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        // 描画物を持たないので何もしない
        // Holds nothing to render
        public void SetEnableRenderers(bool enable)
        {
        }

        private void AddFace(Vector3 center, Vector3 size, Vector3Int localOutwardNormal, BlockDirection blockDirection)
        {
            // 外向き法線を回してワールド面を決める
            // Rotate the outward normal to get the world face
            var faceCollider = gameObject.AddComponent<BoxCollider>();
            faceCollider.isTrigger = true;
            faceCollider.center = center;
            faceCollider.size = size;
            _surfaceTypeByCollider.Add(faceCollider, PreviewSurfaceTypeResolver.Resolve(blockDirection, localOutwardNormal));
        }
    }
}
