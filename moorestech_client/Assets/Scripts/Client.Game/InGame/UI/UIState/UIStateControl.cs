﻿using System;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        public UIStateEnum CurrentState { get; private set; } = UIStateEnum.GameScreen;

        public event Action<UIStateEnum> OnStateChanged;
        private UIStateDictionary _uiStateDictionary;

        [Inject]
        public void Construct(UIStateDictionary uiStateDictionary)
        {
            _uiStateDictionary = uiStateDictionary;
        }

        private void Start()
        {
            _uiStateDictionary.GetState(CurrentState).OnEnter(UIStateEnum.Current);
        }

        //UIステート
        private void Update()
        {
            //UIステートが変更されたら
            var state = _uiStateDictionary.GetState(CurrentState).GetNext();
            if (state == UIStateEnum.Current) return;

            var lastState = CurrentState;
            CurrentState = state;

            //現在のUIステートを終了し、次のステートを呼び出す
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(lastState);

            OnStateChanged?.Invoke(CurrentState);
        }
    }
}