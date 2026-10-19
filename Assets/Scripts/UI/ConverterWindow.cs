using ProjectMayham.Shelter;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// The window of a <see cref="ModuleConverter"/>: what it holds, what is ready, how many pieces of the input to
    /// load (−/+, the arrows or the wheel; all that fits at first) and buttons to load them out of the backpack and
    /// the stash and to take what is ready into them.
    /// </summary>
    public class ConverterWindow : ModalWindow
    {
        private GameObject actor;
        private ModuleConverter converter;
        private int amount;

        private TMP_Text titleLabel;
        private TMP_Text aboutLabel;
        private TMP_Text stateLabel;
        private TMP_Text amountLabel;
        private TMP_Text statusLabel;
        private Button minusButton;
        private Button plusButton;
        private Button allButton;
        private Button loadButton;
        private Button collectButton;

        protected override int SortingOrder => 200;

        public static void Open(GameObject actor, ModuleConverter converter)
        {
            if (actor == null || converter == null) return;
            var window = Get<ConverterWindow>();
            if (window.IsOpen) return;
            window.actor = actor;
            window.converter = converter;
            window.Show();
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 530f));

            titleLabel = UIKit.Label(panel, string.Empty, 34f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(664f, 46f));
            titleLabel.rectTransform.pivot = new Vector2(0f, 1f);

            aboutLabel = UIKit.Label(panel, string.Empty, 20f, TextAlignmentOptions.TopLeft, UIKit.Muted);
            UIKit.Place(aboutLabel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -70f), new Vector2(664f, 80f));
            aboutLabel.rectTransform.pivot = new Vector2(0f, 1f);

            stateLabel = UIKit.Label(panel, string.Empty, 24f, TextAlignmentOptions.TopLeft);
            UIKit.Place(stateLabel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -160f), new Vector2(664f, 130f));
            stateLabel.rectTransform.pivot = new Vector2(0f, 1f);

            // How many to load: − N + and "all".
            var ask = UIKit.Label(panel, "Сколько загрузить:", 24f, TextAlignmentOptions.Left);
            Corner(ask.rectTransform, new Vector2(28f, 150f), new Vector2(240f, 52f));
            minusButton = UIKit.MakeButton(panel, "−", () => Change(-1), 30f);
            Corner((RectTransform)minusButton.transform, new Vector2(276f, 150f), new Vector2(56f, 52f));
            amountLabel = UIKit.Label(panel, string.Empty, 30f, TextAlignmentOptions.Center);
            Corner(amountLabel.rectTransform, new Vector2(336f, 150f), new Vector2(80f, 52f));
            plusButton = UIKit.MakeButton(panel, "+", () => Change(1), 30f);
            Corner((RectTransform)plusButton.transform, new Vector2(420f, 150f), new Vector2(56f, 52f));
            allButton = UIKit.MakeButton(panel, "Всё", () => SetAmount(int.MaxValue), 22f);
            Corner((RectTransform)allButton.transform, new Vector2(488f, 150f), new Vector2(110f, 52f));

            statusLabel = UIKit.Label(panel, string.Empty, 20f, TextAlignmentOptions.Left, UIKit.Muted);
            Corner(statusLabel.rectTransform, new Vector2(28f, 92f), new Vector2(664f, 34f));

            loadButton = UIKit.MakeButton(panel, "Загрузить", Load, 22f);
            Corner((RectTransform)loadButton.transform, new Vector2(28f, 24f), new Vector2(214f, 52f));
            collectButton = UIKit.MakeButton(panel, "Забрать", Collect, 22f);
            Corner((RectTransform)collectButton.transform, new Vector2(258f, 24f), new Vector2(214f, 52f));
            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 22f);
            var closeRect = (RectTransform)close.transform;
            UIKit.Place(closeRect, new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(214f, 52f));
            closeRect.pivot = new Vector2(1f, 0f);
        }

        // Placed from the bottom-left corner of the panel.
        private static void Corner(RectTransform rect, Vector2 position, Vector2 size)
        {
            UIKit.Place(rect, Vector2.zero, position, size);
            rect.pivot = Vector2.zero;
        }

        protected override void OnOpened()
        {
            statusLabel.text = string.Empty;
            converter.Changed += Redraw;
            amount = int.MaxValue; // all that fits
            Redraw();
        }

        protected override void OnClosed()
        {
            if (converter != null) converter.Changed -= Redraw;
            converter = null;
            actor = null;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen || converter == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) Change(1);
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) Change(-1);
            }
            var mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (wheel > 0.01f) Change(1);
            else if (wheel < -0.01f) Change(-1);
        }

        private int Loadable => converter.Loadable(ModuleCost.StoresOf(actor));

        private void Change(int step) => SetAmount(amount + step);

        // The number stays between one and what can go in now.
        private void SetAmount(int value)
        {
            amount = Mathf.Clamp(value, 1, Mathf.Max(1, Loadable));
            Redraw();
        }

        private void Redraw()
        {
            if (converter == null) return;
            var state = converter.State;
            var input = converter.Input;
            var output = converter.Output;
            int have = ModuleCost.Count(input, ModuleCost.StoresOf(actor));
            int loadable = Loadable;
            amount = Mathf.Clamp(amount, 1, Mathf.Max(1, loadable));

            titleLabel.text = converter.Title;
            aboutLabel.text = converter.Description;
            string loaded = state.loaded > 0
                ? $"Загружено: {input.DisplayName} ×{state.loaded} из {converter.Capacity} — будет готово после рейда"
                : $"Загружено: ничего (входит до {converter.Capacity})";
            string ready = state.ready > 0 ? $"Готово: {output.DisplayName} ×{state.ready}" : "Готово: ничего";
            stateLabel.text = $"{loaded}\n{ready}\nУ тебя: {input.DisplayName} ×{have} (рюкзак и склад)";

            bool canLoad = loadable > 0;
            amountLabel.text = canLoad ? amount.ToString() : "0";
            minusButton.interactable = canLoad && amount > 1;
            plusButton.interactable = canLoad && amount < loadable;
            allButton.interactable = canLoad && amount < loadable;
            loadButton.interactable = canLoad;
            UIKit.SetButtonText(loadButton, canLoad ? $"Загрузить {amount}" : "Загрузить");
            collectButton.interactable = state.ready > 0;
        }

        private void Load()
        {
            if (converter == null) return;
            int count = converter.Load(ModuleCost.StoresOf(actor), amount);
            statusLabel.text = count > 0 ? $"Загружено: {converter.Input.DisplayName} ×{count}" : "Нечего загрузить или нет места";
            amount = int.MaxValue; // next time again all that fits
            Redraw();
        }

        private void Collect()
        {
            if (converter == null) return;
            int count = converter.Collect(ModuleCost.StoresOf(actor));
            statusLabel.text = count > 0
                ? $"Забрано: {converter.Output.DisplayName} ×{count}" + (converter.State.ready > 0 ? ". Остальное не влезло" : string.Empty)
                : "Некуда положить: рюкзак и склад полны";
            Redraw();
        }
    }
}
