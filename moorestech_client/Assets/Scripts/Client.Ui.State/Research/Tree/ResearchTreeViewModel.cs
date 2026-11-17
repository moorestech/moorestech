using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリー画面の表示（等価なし）
    /// Research tree display (no equality)
    /// </summary>
    public sealed class ResearchTreeViewModel
    {
        public static readonly ResearchTreeViewModel Empty = Create(Array.Empty<ResearchNodeCardViewModel>(), ResearchFocusPoint.None);

        private readonly ResearchFocusPoint _focus;
        private readonly bool _hasDetail;
        private readonly ResearchDetailViewModel _detail;

        public IReadOnlyList<ResearchNodeCardViewModel> Nodes { get; }

        private ResearchTreeViewModel(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus, bool hasDetail, ResearchDetailViewModel detail)
        {
            Nodes = nodes;
            _focus = focus;
            _hasDetail = hasDetail;
            _detail = detail;
        }

        public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus)
        {
            return new ResearchTreeViewModel(nodes, focus, false, null);
        }

        public static ResearchTreeViewModel CreateWithDetail(IReadOnlyList<ResearchNodeCardViewModel> nodes, ResearchFocusPoint focus, ResearchDetailViewModel detail)
        {
            return new ResearchTreeViewModel(nodes, focus, true, detail);
        }

        public bool TryGetInitialFocus(out Vector2 treePoint)
        {
            return _focus.TryGetPoint(out treePoint);
        }

        public bool TryGetDetail(out ResearchDetailViewModel detail)
        {
            detail = _detail;
            return _hasDetail;
        }
    }

    /// <summary>
    /// 初期の注目点（無いことを型で持つ）
    /// Initial focus point (absence in the type)
    /// </summary>
    public readonly struct ResearchFocusPoint
    {
        public static ResearchFocusPoint None => new(false, Vector2.zero);

        private readonly bool _hasPoint;
        private readonly Vector2 _point;

        private ResearchFocusPoint(bool hasPoint, Vector2 point)
        {
            _hasPoint = hasPoint;
            _point = point;
        }

        public static ResearchFocusPoint At(Vector2 treePoint)
        {
            return new ResearchFocusPoint(true, treePoint);
        }

        public bool TryGetPoint(out Vector2 treePoint)
        {
            treePoint = _point;
            return _hasPoint;
        }
    }
}
