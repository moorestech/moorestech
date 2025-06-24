using System.Collections.Generic;
using Client.Skit.Skit;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Client.Skit.UI
{
    public class SkitUI : MonoBehaviour
    {
        [SerializeField] private UIDocument skitUiDocument;
        
        [Inject] ISkitActionContext _skitActionContext;
        
        private SkitUITools _skitUITools;
        
        private void Start()
        {
            _skitUITools = new SkitUITools(skitUiDocument, _skitActionContext);
        }
        
        public void SetText(string characterName, string text)
        {
            GetElement<Label>("SpeakerName").text = characterName;
            GetElement<Label>("MainText").text = text;
        }
        
        public void ShowTransition(bool isShow, float duration)
        {
            // UXML / UI Builder で Name = "Transition" を付けておく
            var transition = GetElement<Label>("Transition");
            if (transition == null) return;               // 念のため存在チェック
            
            // 0 秒なら即座に切り替え
            if (duration <= 0f)
            {
                transition.style.display = isShow ? DisplayStyle.Flex : DisplayStyle.None;
                transition.style.opacity = isShow ? 1f : 0f;
                return;
            }
            
            // Fire-and-forget でフェード処理
            FadeAsync().Forget();
            
            #region Internal
            
            async UniTaskVoid FadeAsync()
            {
                // 表示に切り替えるときは最初に display を有効にしておく
                if (isShow)
                {
                    transition.style.display = DisplayStyle.Flex;
                    transition.style.opacity = 0f;
                }
                
                var startAlpha = transition.resolvedStyle.opacity;
                var endAlpha = isShow ? 1f : 0f;
                var t = 0f;
                
                // 経過時間に合わせて α を補間
                while (t < duration)
                {
                    t += Time.deltaTime;
                    var lerp = Mathf.Clamp01(t / duration);
                    transition.style.opacity = Mathf.Lerp(startAlpha, endAlpha, lerp);
                    
                    await UniTask.Yield(PlayerLoopTiming.Update); // 1 フレーム待つ
                }
                
                transition.style.opacity = endAlpha;
                
                // フェードアウト後は完全に非表示
                if (!isShow)
                {
                    transition.style.display = DisplayStyle.None;
                }
            }
            
            #endregion
        }

        
        public void ShowSelectionUI(bool enable)
        {
            var selections = GetElement<VisualElement>("Selections");
            if (selections != null)
            {
                selections.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        public async UniTask<int> WaitSelectText(List<string> texts)
        {
            var selections = GetElement<VisualElement>("Selections");
            if (selections == null) return -1;
            
            var buttons = selections.Query<Button>("SelectButton").ToList();
            
            // 全てのボタンを非表示にする
            foreach (var button in buttons)
            {
                button.style.display = DisplayStyle.None;
            }
            
            // テキストの数だけボタンを表示
            for (int i = 0; i < texts.Count && i < buttons.Count; i++)
            {
                buttons[i].text = texts[i];
                buttons[i].style.display = DisplayStyle.Flex;
            }
            
            // 選択を待つ
            var tcs = new UniTaskCompletionSource<int>();
            var callbacks = new List<System.Action>();
            
            for (int i = 0; i < texts.Count && i < buttons.Count; i++)
            {
                int index = i; // クロージャーのためのローカル変数
                System.Action callback = () => tcs.TrySetResult(index);
                callbacks.Add(callback);
                buttons[i].clicked += callback;
            }
            
            try
            {
                return await tcs.Task;
            }
            finally
            {
                // クリーンアップ：イベントハンドラーを削除
                for (int i = 0; i < texts.Count && i < buttons.Count; i++)
                {
                    buttons[i].clicked -= callbacks[i];
                }
            }
        }
        
        private void Update()
        {
            _skitUITools.ManualUpdate();
        }
        
        private T GetElement<T>(string elementName) where T : VisualElement
        {
            return skitUiDocument.rootVisualElement.Q<T>(elementName);
        }
        
        public void ShowTextArea(bool isActive)
        {
            var textArea = GetElement<VisualElement>("TextArea");
            if (textArea != null)
            {
                textArea.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}