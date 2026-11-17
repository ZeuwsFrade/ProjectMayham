using ProjectMayham.Localization;
using ProjectMayham.Shelter;
using TMPro;
using UnityEngine;

namespace ProjectMayham.UI
{
    public class CoffeeMachineWindow : ModalWindow
    {
        private GameObject actor;
        private CoffeeMachine machine;
        private TMP_Text stateLabel, statusLabel;
        private UnityEngine.UI.Button waterButton, beansButton, collectButton;
        protected override int SortingOrder => 200;

        public static void Open(GameObject actor, CoffeeMachine machine)
        {
            if (actor == null || machine == null) return;
            var window = Get<CoffeeMachineWindow>();
            if (window.IsOpen) return;
            window.actor = actor;
            window.machine = machine;
            window.Show();
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);
            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(780, 580));
            Label(panel, GameText.Translate("Кофемашина"), 32, 26, 22, 728, 44, UIKit.Accent);
            Label(panel, GameText.Translate("1 вода + 1 использование зёрен → 1 кофе за рейд.\nПорции зёрен хватает на три приготовления. Без ресурсов машина ждёт."),
                22, 26, 82, 728, 92, UIKit.Muted);
            stateLabel = Label(panel, "", 24, 26, 190, 728, 160, Color.white);
            statusLabel = Label(panel, "", 20, 26, 366, 728, 58, UIKit.Muted);
            waterButton = UIKit.MakeButton(panel, "Долить воду", LoadWater, 22);
            Place((RectTransform)waterButton.transform, 26, 440, 350, 52);
            beansButton = UIKit.MakeButton(panel, "Засыпать зёрна", LoadBeans, 22);
            Place((RectTransform)beansButton.transform, 404, 440, 350, 52);
            collectButton = UIKit.MakeButton(panel, "Забрать кофе", Collect, 22);
            Place((RectTransform)collectButton.transform, 26, 508, 350, 48);
            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 22);
            Place((RectTransform)close.transform, 404, 508, 350, 48);
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h) =>
            UIKit.Place(rect, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

        private static TMP_Text Label(Transform parent, string text, float size, float x, float y, float w, float h, Color color)
        {
            var label = UIKit.Label(parent, text, size, TextAlignmentOptions.TopLeft, color);
            Place(label.rectTransform, x, y, w, h);
            return label;
        }

        protected override void OnOpened()
        {
            statusLabel.text = GameText.Translate("Ресурсы берутся из рюкзака и со склада.");
            machine.Changed += Redraw;
            Redraw();
        }

        protected override void OnClosed()
        {
            if (machine != null) machine.Changed -= Redraw;
            machine = null;
            actor = null;
        }

        private void Redraw()
        {
            if (machine == null) return;
            var state = machine.State;
            var stores = ModuleCost.StoresOf(actor);
            string next = state.loaded > 0 && state.beanUses > 0 ? GameText.Translate("Следующий кофе будет готов после рейда.")
                : state.beanUses == 0 ? GameText.Translate("Нужна новая порция зёрен.") : GameText.Translate("Долей воду, чтобы продолжить.");
            stateLabel.text = GameText.Format("Вода: {0}/{1}\nЗёрна: {2}/{3} использований\nГотовый кофе: {4}\n{5}", state.loaded, CoffeeMachine.WaterCapacity, state.beanUses, CoffeeMachine.UsesPerPortion, state.ready, next);
            int water = machine.LoadableWater(stores);
            waterButton.interactable = water > 0;
            UIKit.SetButtonText(waterButton, water > 0 ? GameText.Format("Долить воду ×{0}", water) : GameText.Translate("Долить воду"));
            beansButton.interactable = machine.CanLoadBeans(stores);
            collectButton.interactable = state.ready > 0;
        }

        private void LoadWater()
        {
            int count = machine.LoadWater(ModuleCost.StoresOf(actor), CoffeeMachine.WaterCapacity);
            statusLabel.text = count > 0 ? GameText.Format("Добавлено воды: {0}.", count) : GameText.Translate("Нет воды или бак полон.");
            Redraw();
        }

        private void LoadBeans()
        {
            statusLabel.text = machine.LoadBeans(ModuleCost.StoresOf(actor))
                ? GameText.Translate("Зёрна загружены: хватит на три кофе.") : GameText.Translate("Нет зёрен или предыдущая порция ещё не закончилась.");
            Redraw();
        }

        private void Collect()
        {
            int count = machine.Collect(ModuleCost.StoresOf(actor));
            statusLabel.text = count > 0 ? GameText.Format("Забрано кофе: {0}. Осталось в машине: {1}.", count, machine.State.ready) : GameText.Translate("В рюкзаке и на складе нет места.");
            Redraw();
        }
    }
}
