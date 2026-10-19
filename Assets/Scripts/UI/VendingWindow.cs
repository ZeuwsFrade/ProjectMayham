using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// The window of a vending machine: what is inside, a coin a piece, and — with the skill — a button to shake it.
    /// The game goes on meanwhile, like when searching a container: the player stands still facing the machine, and
    /// a step, E or a blow closes the window. Things paid for come out by themselves after a moment.
    /// </summary>
    public class VendingWindow : ModalWindow
    {
        private const float MaxDistance = 2.5f;

        private GameObject actor;
        private VendingMachine machine;
        private PlayerController controller;
        private PlayerStats stats;
        private int openedFrame;
        private bool moveReleased;

        private TMP_Text coinsLabel;
        private TMP_Text statusLabel;
        private RectTransform list;
        private Button shakeButton;

        protected override int SortingOrder => 90;
        protected override bool PausesGame => false;

        public static void Open(GameObject actor, VendingMachine machine)
        {
            if (actor == null || machine == null) return;
            var window = Get<VendingWindow>();
            if (window.IsOpen) return;
            window.actor = actor;
            window.machine = machine;
            window.Show();
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.2f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            var panel = UIKit.Panel(canvas.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(420f, 0f), new Vector2(640f, 620f));

            var title = UIKit.Label(panel, "Автомат", 34f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(360f, 46f));
            title.rectTransform.pivot = new Vector2(0f, 1f);

            coinsLabel = UIKit.Label(panel, string.Empty, 24f, TextAlignmentOptions.Right, UIKit.Muted);
            UIKit.Place(coinsLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(240f, 36f));
            coinsLabel.rectTransform.pivot = new Vector2(1f, 1f);

            statusLabel = UIKit.Label(panel, string.Empty, 22f, TextAlignmentOptions.Left);
            UIKit.Place(statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(592f, 34f));
            statusLabel.rectTransform.pivot = new Vector2(0f, 1f);

            var scroll = UIKit.Rect("ListArea", panel);
            UIKit.Place(scroll, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(592f, 380f));
            scroll.pivot = new Vector2(0.5f, 1f);
            list = UIKit.ScrollList(scroll, "List", out _);
            UIKit.Stretch((RectTransform)list.parent);

            shakeButton = UIKit.MakeButton(panel, "Потрясти", Shake, 24f);
            UIKit.Place((RectTransform)shakeButton.transform, new Vector2(0f, 0f), new Vector2(24f, 64f), new Vector2(220f, 48f));
            ((RectTransform)shakeButton.transform).pivot = new Vector2(0f, 0f);

            var hint = UIKit.Label(panel, "Шаг или E — отойти", 18f, TextAlignmentOptions.Left, UIKit.Muted);
            UIKit.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(592f, 32f));
            hint.rectTransform.pivot = new Vector2(0f, 0f);
        }

        protected override void OnOpened()
        {
            openedFrame = Time.frameCount;
            moveReleased = false;
            statusLabel.text = string.Empty;
            machine.Changed += OnChanged;

            controller = actor.GetComponent<PlayerController>();
            // A locked character keeps the speed it had: stop it, or it slides away from the machine.
            if (actor.TryGetComponent<Rigidbody2D>(out var body)) body.linearVelocity = Vector2.zero;
            if (controller != null)
            {
                controller.MovementLocked = true;
                controller.LookAt = Nearest(machine.Position);
            }
            stats = actor.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.Damaged += OnDamaged;
                stats.Died += Close;
            }
            Redraw();
        }

        protected override void OnClosed()
        {
            if (machine != null) machine.Changed -= OnChanged;
            if (controller != null)
            {
                controller.MovementLocked = false;
                controller.LookAt = null;
            }
            if (stats != null)
            {
                stats.Damaged -= OnDamaged;
                stats.Died -= Close;
            }
            controller = null;
            stats = null;
            machine = null;
            actor = null;
        }

        private void OnDamaged(float amount) => Close();

        private void OnChanged(string text)
        {
            if (!string.IsNullOrEmpty(text)) statusLabel.text = text;
            Redraw();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            if (machine == null || actor == null || ShouldClose()) Close();
        }

        private bool ShouldClose()
        {
            // The press that opened the window is not a press to close it.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && Time.frameCount != openedFrame) return true;

            // A step ends it. A key that was held when the window opened has to be let go first.
            Vector2 move = controller != null ? controller.MoveInput : Vector2.zero;
            if (move.sqrMagnitude < 0.01f) moveReleased = true;
            else if (moveReleased) return true;

            return (Nearest(machine.Position) - (Vector2)actor.transform.position).sqrMagnitude > MaxDistance * MaxDistance;
        }

        // The copy of a point nearest to the player: the level wraps around.
        private Vector2 Nearest(Vector2 point)
        {
            Vector2 origin = actor.transform.position;
            var wrap = WrapWorld.Active;
            return origin + (wrap != null ? wrap.Delta(origin, point) : point - origin);
        }

        private void Redraw()
        {
            if (machine == null) return;
            var backpack = actor.GetComponent<GridInventory>();
            int coins = VendingStock.Coins(backpack, machine.Coin);
            coinsLabel.text = $"Монет: {coins}";

            UIKit.ClearChildren(list);
            var contents = machine.Contents;
            if (contents.IsEmpty)
            {
                var empty = UIKit.Label(list, machine.Coming > 0 ? "Последнее уже выдаётся" : "Внутри пусто", 24f, TextAlignmentOptions.Left, UIKit.Muted);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            }
            foreach (var slot in contents.Slots)
            {
                var item = slot.Item;
                var row = UIKit.Panel(list, "Row", UIKit.RowColor).rectTransform;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

                string name = slot.Count > 1 ? $"{item.DisplayName} ×{slot.Count}" : item.DisplayName;
                var label = UIKit.Label(row, name, 24f, TextAlignmentOptions.Left);
                UIKit.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(14f, 0f);
                label.rectTransform.offsetMax = new Vector2(-300f, 0f);

                var price = UIKit.Label(row, "1 монета", 22f, TextAlignmentOptions.Right, UIKit.Muted);
                UIKit.Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-160f, 0f), new Vector2(130f, 40f));
                price.rectTransform.pivot = new Vector2(1f, 0.5f);

                var buy = UIKit.MakeButton(row, "Купить", () => Buy(item), 22f);
                var rect = (RectTransform)buy.transform;
                UIKit.Place(rect, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(140f, 40f));
                rect.pivot = new Vector2(1f, 0.5f);
                buy.interactable = coins > 0;
            }

            shakeButton.gameObject.SetActive(machine.CanShake);
            shakeButton.interactable = !contents.Jammed && !contents.IsEmpty;
            UIKit.SetButtonText(shakeButton, contents.Jammed ? "Заклинило" : "Потрясти");
        }

        private void Buy(ItemDefinition item)
        {
            if (machine == null) return;
            string refusal = machine.Buy(actor, item);
            statusLabel.text = refusal ?? $"Оплачено: {item.DisplayName}. Ждём…";
            Redraw();
        }

        private void Shake()
        {
            if (machine == null) return;
            statusLabel.text = machine.Shake(actor);
            Redraw();
        }
    }
}
