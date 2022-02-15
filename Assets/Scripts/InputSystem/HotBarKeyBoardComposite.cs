using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
#endif

#if UNITY_EDITOR
namespace InputSystem
{
    [InitializeOnLoad]
#endif
    [DisplayStringFormat("{select0}/{select1}/{select2}/{select3}/{select4}/{select5}/{select6}/{select7}/{select8}")]
    public class HotBarKeyBoardComposite : InputBindingComposite<int>
    {
#if UNITY_EDITOR
        static HotBarKeyBoardComposite()
        {
            Initialize();
        }
#endif

        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            UnityEngine.InputSystem.InputSystem.RegisterBindingComposite<HotBarKeyBoardComposite>();
        }

        [InputControl(layout = "Button")] public int select0 = 0;
        [InputControl(layout = "Button")] public int select1 = 1;
        [InputControl(layout = "Button")] public int select2 = 2;
        [InputControl(layout = "Button")] public int select3 = 3;
        [InputControl(layout = "Button")] public int select4 = 4;
        [InputControl(layout = "Button")] public int select5 = 5;
        [InputControl(layout = "Button")] public int select6 = 6;
        [InputControl(layout = "Button")] public int select7 = 7;
        [InputControl(layout = "Button")] public int select8 = 8;

        public override int ReadValue(ref InputBindingCompositeContext context)
        {
            if (context.ReadValueAsButton(select0))
            {
                return select0;
            }
            if (context.ReadValueAsButton(select1))
            {
                return select1;
            }
            if (context.ReadValueAsButton(select2))
            {
                return select2;
            }
            if (context.ReadValueAsButton(select3))
            {
                return select3;
            }
            if (context.ReadValueAsButton(select4))
            {
                return select4;
            }
            if (context.ReadValueAsButton(select5))
            {
                return select5;
            }
            if (context.ReadValueAsButton(select6))
            {
                return select6;
            }
            if (context.ReadValueAsButton(select7))
            {
                return select7;
            }
            if (context.ReadValueAsButton(select8))
            {
                return select8;
            }

            return 0;
        }
    }
}