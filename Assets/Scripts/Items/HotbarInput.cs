using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectMayham.Items
{
    /// <summary>Selects hotbar slots with the number keys (1..N) and the mouse wheel.</summary>
    [RequireComponent(typeof(Inventory))]
    public class HotbarInput : MonoBehaviour
    {
        private Inventory inventory;

        private void Awake() => inventory = GetComponent<Inventory>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                int keys = Mathf.Min(inventory.SlotCount, 9);
                for (int i = 0; i < keys; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame) inventory.Select(i);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) inventory.Cycle(-1);
                else if (scroll < -0.1f) inventory.Cycle(1);
            }
        }
    }
}
