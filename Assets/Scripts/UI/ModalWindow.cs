using ProjectMayham.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Base of the windows that are built from code and pause the game while they are open (trade, stash,
    /// dialogue, confirmations). The window creates its own canvas on first use; Esc closes it.
    /// </summary>
    public abstract class ModalWindow : MonoBehaviour
    {
        // Open windows, the last one is on top.
        private static readonly System.Collections.Generic.List<ModalWindow> open = new System.Collections.Generic.List<ModalWindow>();
        private static int escapeHandledFrame = -1;

        private Canvas canvas;

        public bool IsOpen { get; private set; }

        /// <summary>True when a modal window used this frame's Esc press (so the game must not treat it as its own).</summary>
        public static bool EscapeConsumed => escapeHandledFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => open.Clear();

        /// <summary>Canvas sorting order: later windows are drawn over earlier ones.</summary>
        protected abstract int SortingOrder { get; }

        /// <summary>Creates the UI once; the canvas to put it under is passed in.</summary>
        protected abstract void Build(Canvas canvas);

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        /// <summary>The window that exists in the scene, or a new one.</summary>
        public static T Get<T>() where T : ModalWindow
        {
            var found = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found != null) return found;
            return new GameObject(typeof(T).Name).AddComponent<T>();
        }

        protected void Show()
        {
            if (IsOpen) return;
            if (canvas == null)
            {
                canvas = UIKit.NewCanvas(GetType().Name, SortingOrder, transform);
                Build(canvas);
            }
            canvas.gameObject.SetActive(true);
            IsOpen = true;
            open.Add(this);
            ModalState.Push();
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            open.Remove(this);
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalState.Pop();
            OnClosed();
        }

        /// <summary>Esc was pressed while the window is open; closes by default.</summary>
        protected virtual void OnCancel() => Close();

        protected virtual void Update()
        {
            if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            // Only the window on top reacts, and one Esc press closes one window.
            if (open.Count == 0 || open[open.Count - 1] != this || escapeHandledFrame == Time.frameCount) return;
            escapeHandledFrame = Time.frameCount;
            OnCancel();
        }

        protected virtual void OnDisable()
        {
            if (IsOpen) Close();
        }
    }
}
