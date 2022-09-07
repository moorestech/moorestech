using UnityEngine;

namespace MainGame.UnityView.Entity
{
    public class ItemEntityObject : MonoBehaviour, IEntityObject
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material itemMaterial;

        public void SetTexture(Texture texture)
        {
            var material = new Material(itemMaterial)
            {
                mainTexture = texture
            };
            meshRenderer.material = material;
        }
+
        public void SetPosition(Vector3 position)
        {
            
        }
    }
}