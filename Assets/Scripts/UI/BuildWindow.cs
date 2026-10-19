using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Shelter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>
    /// The window of a free place in the shelter: the modules whose blueprints the player knows, each with what it
    /// takes (counted in the backpack and the stash together) and a button to build it there.
    /// </summary>
    public class BuildWindow : ModalWindow
    {
        private const string NoBlueprints =
            "Ты не знаешь ни одного чертежа. Чертежи дают за задания, а иногда их находят в рейдах.";

        private GameObject actor;
        private ShelterModuleSlot slot;
        private TMP_Text statusLabel;
        private RectTransform list;

        protected override int SortingOrder => 200;

        public static void Open(GameObject actor, ShelterModuleSlot slot)
        {
            if (actor == null || slot == null) return;
            var window = Get<BuildWindow>();
            if (window.IsOpen) return;
            window.actor = actor;
            window.slot = slot;
            window.Show();
        }

        protected override void Build(Canvas canvas)
        {
            var dim = UIKit.Panel(canvas.transform, "Dim", UIKit.Dim);
            UIKit.Stretch(dim.rectTransform);

            var panel = UIKit.Panel(dim.transform, "Panel", UIKit.PanelColor).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 680f));

            var title = UIKit.Label(panel, "Место под модуль", 34f, TextAlignmentOptions.Left, UIKit.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(760f, 46f));
            title.rectTransform.pivot = new Vector2(0f, 1f);

            statusLabel = UIKit.Label(panel, string.Empty, 22f, TextAlignmentOptions.Left, UIKit.Muted);
            UIKit.Place(statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -66f), new Vector2(764f, 34f));
            statusLabel.rectTransform.pivot = new Vector2(0f, 1f);

            var area = UIKit.Rect("ListArea", panel);
            UIKit.Place(area, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(764f, 470f));
            area.pivot = new Vector2(0.5f, 1f);
            list = UIKit.ScrollList(area, "List", out _);
            UIKit.Stretch((RectTransform)list.parent);

            var close = UIKit.MakeButton(panel, "Закрыть (Esc)", Close, 24f);
            var closeRect = (RectTransform)close.transform;
            UIKit.Place(closeRect, new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(240f, 52f));
            closeRect.pivot = new Vector2(1f, 0f);
        }

        protected override void OnOpened()
        {
            statusLabel.text = "Ресурсы берутся из рюкзака и со склада.";
            Redraw();
        }

        protected override void OnClosed()
        {
            actor = null;
            slot = null;
        }

        private void Redraw()
        {
            UIKit.ClearChildren(list);
            var stores = ModuleCost.StoresOf(actor);
            var known = slot.Available.Where(Blueprints.IsKnown).ToList();
            if (known.Count == 0)
            {
                var none = UIKit.Label(list, NoBlueprints, 24f, TextAlignmentOptions.Left, UIKit.Muted);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;
                return;
            }
            foreach (var module in known) AddRow(module, stores);
        }

        private void AddRow(ShelterModuleDefinition module, List<GridInventory> stores)
        {
            var row = UIKit.Panel(list, "Row", UIKit.RowColor).rectTransform;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;

            var name = UIKit.Label(row, module.DisplayName, 28f, TextAlignmentOptions.TopLeft, UIKit.Accent);
            Fill(name.rectTransform, 12f, 220f, 10f, 40f);

            var about = UIKit.Label(row, module.Description, 19f, TextAlignmentOptions.TopLeft, UIKit.Muted);
            Fill(about.rectTransform, 12f, 220f, 48f, 52f);

            var cost = UIKit.Label(row, CostText(module, stores), 20f, TextAlignmentOptions.TopLeft);
            Fill(cost.rectTransform, 12f, 220f, 104f, 40f);

            string refusal = slot.CannotBuild(module, stores);
            if (refusal == null)
            {
                var build = UIKit.MakeButton(row, "Построить", () => Build(module), 22f);
                var rect = (RectTransform)build.transform;
                UIKit.Place(rect, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(190f, 52f));
                rect.pivot = new Vector2(1f, 0.5f);
            }
            else
            {
                var why = UIKit.Label(row, refusal, 20f, TextAlignmentOptions.Right, UIKit.Bad);
                UIKit.Place(why.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(200f, 52f));
                why.rectTransform.pivot = new Vector2(1f, 0.5f);
            }
        }

        // Stretched across the row with its right part left for the button, at a height from the top.
        private static void Fill(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // "Нужно: Папки 2/3 · Швабра 1/1", what is missing in red.
        private static string CostText(ShelterModuleDefinition module, List<GridInventory> stores)
        {
            var parts = new List<string>();
            foreach (var part in module.Cost)
            {
                if (part == null || part.item == null) continue;
                int need = ModuleCost.Need(part), have = ModuleCost.Count(part.item, stores);
                string line = $"{part.item.DisplayName} {Mathf.Min(have, need)}/{need}";
                parts.Add(have >= need ? line : $"<color=#FF7366>{line}</color>");
            }
            return parts.Count == 0 ? "Ничего не нужно" : "Нужно: " + string.Join("  ·  ", parts);
        }

        private void Build(ShelterModuleDefinition module)
        {
            if (slot == null) return;
            if (!slot.TryBuild(module, ModuleCost.StoresOf(actor)))
            {
                statusLabel.text = slot.CannotBuild(module, ModuleCost.StoresOf(actor)) ?? "Не получилось";
                Redraw();
                return;
            }
            Close();
            ScreenNote.Show($"Построено: {module.DisplayName}", 3f);
        }
    }
}
