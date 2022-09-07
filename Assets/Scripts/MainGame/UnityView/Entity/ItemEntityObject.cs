using UnityEngine;

namespace MainGame.UnityView.Entity
{
    public class ItemEntityObject : MonoBehaviour
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
    }
}