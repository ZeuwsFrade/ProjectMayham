using System;
using ProjectMayham.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    /// <summary>Row of inventory slots at the bottom of the screen with the selected one highlighted.</summary>
    public class HotbarUI : MonoBehaviour
    {
        [Serializable]
        public class SlotView
        {
            public Image frame;
            public Image icon;
            public TMP_Text count;
        }

        [SerializeField] private Inventory inventory;
        [SerializeField] private SlotView[] slots;

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.Changed += Refresh;
            inventory.SelectionChanged += OnSelectionChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.Changed -= Refresh;
            inventory.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(int _) => Refresh();

        private void Refresh()
        {
            for (int i = 0; i < slots.Length && i < inventory.SlotCount; i++)
            {
                var slot = inventory[i];
                var view = slots[i];

                view.frame.enabled = i == inventory.Selected;
                view.icon.enabled = !slot.IsEmpty;
                view.icon.sprite = slot.IsEmpty ? null : slot.item.Icon;
                view.count.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
            }
        }
    }
}
