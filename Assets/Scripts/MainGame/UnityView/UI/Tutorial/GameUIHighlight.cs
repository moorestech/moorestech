using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.UIState.UIObject;
using UnityEngine;

namespace MainGame.UnityView.UI.Tutorial
{
    public class GameUIHighlight : MonoBehaviour
    {
        [SerializeField] private RectTransformHighlightCreator rectTransformHighlightCreator;

        [CanBeNull] delegate RectTransformReadonlyData RectTransformGetAction();
        
        /// <summary>
        /// 各タイプに応じた <see cref="RectTransformReadonlyData"/> を返すアクションを定義する
        /// わざわざアクションを定義する理由は、動的生成されるUIの場合、SerializeFieldで定義取得することができないため
        /// そのようなオブジェクトでも取得できるようにするためにこうする
        /// </summary>
        private readonly Dictionary<HighlightType,RectTransformGetAction> _rectTransformGetActions = new();

        [SerializeField] private RectTransform craftItemPutButton;
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        
        /// <summary>
        /// アクティブなハイライトのオブジェクトを保持する
        /// TODO ないということは、ハイライトがオフになっているということ　これよくないと思うのでリファクタした方が良さそう
        /// </summary>
        private readonly Dictionary<HighlightType, IRectTransformHighlightObject> _activeHighlightObjects = new();
        /// <summary>
        /// <see cref="_activeHighlightObjects"/>のキーを保持する
        /// わざわざ別でリストを保持する理由は、マイフレームキーのリストを生成して破棄するのはパフォーマンス上問題が発生しそうなので、
        /// 追加、削除のタイミングのみで更新を行うようにする
        /// </summary>
        private List<HighlightType> _rectTransformHighlightObjectKeys = new();

        private void Start()
        {
            var itemPutButtonReadonly = new RectTransformReadonlyData(craftItemPutButton);
            _rectTransformGetActions.Add(HighlightType.CraftItemPutButton, () => itemPutButtonReadonly);
            _rectTransformGetActions.Add(HighlightType.CraftResultSlot, () => playerInventorySlots.GetSlotRect(CraftInventoryObjectCreator.ResultSlotName));
        }

        public void SetHighlight(HighlightType highlightType,bool isActive)
        {
            var isExist = _activeHighlightObjects.TryGetValue(highlightType, out var highlightObject);

            switch (isExist)
            {
                //ハイライトがない場合でオンにする場合は作成
                case false when isActive:
                {
                    _activeHighlightObjects.Add(highlightType, null);
                    CreateAndSetTransformHighlightObject(highlightType);
                    break;
                }
                //ハイライトがあって、オフにする場合は削除
                case true when !isActive:
                    highlightObject?.Destroy();
                    _activeHighlightObjects.Remove(highlightType);
                    break;
            }
            //Dictionaryが変更されたのでキーのリストを更新
            _rectTransformHighlightObjectKeys = _activeHighlightObjects.Keys.ToList();
        }

        
        /// <summary>
        /// オブジェクトの更新、作成できる場合は作成する
        /// </summary>
        private void Update()
        {
            foreach (var key in _rectTransformHighlightObjectKeys)
            {
                //nullか、ハイライトのターゲットが破棄されている場合は新しく作る
                if (_activeHighlightObjects[key] == null || 
                    _activeHighlightObjects[key] != null && _activeHighlightObjects[key].IsTargetDestroyed)
                {
                    CreateAndSetTransformHighlightObject(key);
                }
            }
            
        }

        /// <summary>
        /// そのオブジェクトに対するハイライトの作成を試みる
        /// 作成できた場合は<see cref="_activeHighlightObjects"/>に格納する
        /// 作成できない場合はnullを格納する
        /// </summary>
        /// <param name="highlightType"></param>
        private void CreateAndSetTransformHighlightObject(HighlightType highlightType)
        {
            //nullの場合はオブジェクトの作成を試みる
            var readonlyData = _rectTransformGetActions[highlightType]();
            if (readonlyData != null)
            {
                _activeHighlightObjects[highlightType] = rectTransformHighlightCreator.CreateHighlightObject(readonlyData);
            }
            else
            {
                _activeHighlightObjects[highlightType] = null;
            }
        }
    }
    
    public enum HighlightType
    {
        CraftItemPutButton,
        CraftResultSlot,
    }
}