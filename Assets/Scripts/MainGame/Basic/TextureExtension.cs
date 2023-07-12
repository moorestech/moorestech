using UnityEngine;

namespace MainGame.Basic
{
    public static class TextureExtension
    {
        public static Sprite ToSprite(this Texture2D texture2D)
        {
            return texture2D == null ? null : Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.zero);
        }
    }
}