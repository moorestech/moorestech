using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Skit.UI
{
    public class SelectionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text buttonText;

        private int _index;

        public void SetButton(string text, int index)
        {
            buttonText.text = text;
            _index = index;
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public async UniTask<int> WaitClick(CancellationToken ct)
        {
            await button.OnClickAsync(ct);
            return _index;
        }
    }
}