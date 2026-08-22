using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// アイコン配信の対象種別（Item/Block/TrainCar等）を1本の窓口へ揃えるための抽象
    /// Abstraction that unifies each icon kind (Item/Block/TrainCar, etc.) behind one delivery path
    /// </summary>
    public interface IIconTextureSource
    {
        // ルーティング判定に使う経路プレフィクス（例: "/api/icons/"）
        // Path prefix used for routing (e.g. "/api/icons/")
        string PathPrefix { get; }

        // 該当 ImageContainer が生成済みかどうか
        // Whether the backing ImageContainer has been created yet
        bool IsReady { get; }

        /// <summary>
        /// キー文字列が自種別のID書式として妥当か。任意文字列がキャッシュへ入るのを防ぐため解決前に判定する
        /// Whether the key text is a well-formed id for this kind; checked before resolving so arbitrary strings never reach the cache
        /// </summary>
        bool IsValidKey(string keyText);

        /// <summary>
        /// キー文字列（int/Guid等）をパースしテクスチャを解決する。メインスレッドで呼ばれる契約
        /// Parses the key text (int/Guid, etc.) and resolves the texture; must be called on the main thread
        /// </summary>
        Texture2D ResolveOrNull(string keyText);
    }
}
