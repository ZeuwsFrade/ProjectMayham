using ProjectMayham.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.UI
{
    /// <summary>
    /// Base of the windows that are built from code and pause the game while they are open (trade, stash,
    /// dialogue, confirmations); a window may let the game go on instead (<see cref="PausesGame"/>). The window
    /// creates its own canvas on first use; Esc closes it.
    /// </summary>
    public abstract class ModalWindow : MonoBehaviour
    {
        // Open windows, the last one is on top.
        private static readonly System.Collections.Generic.List<ModalWindow> open = new System.Collections.Generic.List<ModalWindow>();
        private static int escapeHandledFrame = -1;

        private Canvas canvas;
        // What the window told ModalState when it opened, to tell it the same on closing.
        private bool pausedGame;

        public bool IsOpen { get; private set; }

        /// <summary>False for a window behind which the game goes on. Asked every time the window opens.</summary>
        protected virtual bool PausesGame => true;

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
            // Exactly this type: a window that extends another one is not that other window.
            foreach (var found in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found.GetType() == typeof(T)) return found;
            }
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
            pausedGame = PausesGame;
            ModalState.Push(pausedGame);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            open.Remove(this);
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalState.Pop(pausedGame);
            OnClosed();
        }

        /// <summary>
        /// Throws the built UI away, so that it is built again the next time the window opens: for a window whose
        /// layout depends on what it shows. Does nothing while the window is open.
        /// </summary>
        protected void DiscardUI()
        {
            if (IsOpen || canvas == null) return;
            Destroy(canvas.gameObject);
            canvas = null;
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
