using System;
using System.Collections;
using System.Linq;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.World;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A vending machine in a raid. E opens its window (<see cref="VendingWindow"/>): what is inside, a coin a piece.
    /// What is bought comes out after a couple of seconds: into the backpack when the player is still at the machine
    /// and it fits, else onto the floor in front of the machine; the clatter is heard by enemies nearby. With the
    /// skill <see cref="PlayerSkills.VendingShake"/>
    /// the machine can be shaken: it costs stamina and makes noise, and sometimes gives a thing for free; after a few
    /// failures it jams. What is inside is rolled at the start of the raid (<see cref="VendingStock"/>).
    /// </summary>
    public class VendingMachine : MonoBehaviour, IInteractable
    {
        [Header("Contents")]
        [Tooltip("What may be inside.")]
        [SerializeField] private LootTable stock;
        [SerializeField, Min(0)] private int minItems = 3;
        [SerializeField, Min(0)] private int maxItems = 6;
        [Tooltip("Chance that a machine is empty.")]
        [SerializeField, Range(0f, 1f)] private float emptyChance = 0.15f;
        [Tooltip("What the machine takes: one of these a thing.")]
        [SerializeField] private ItemDefinition coin;

        [Header("Giving out")]
        [Tooltip("Seconds from paying until the thing comes out: from and to.")]
        [SerializeField] private Vector2 dropDelay = new Vector2(2f, 3f);
        [Tooltip("A player closer than this when the thing comes out gets it into the backpack.")]
        [SerializeField, Min(0.5f)] private float reach = 2.5f;
        [Tooltip("How far the clatter of a thing coming out is heard by enemies, in world units (quieter than a shake).")]
        [SerializeField, Min(0f)] private float dropNoise = 3.5f;

        [Header("Shaking (a skill)")]
        [SerializeField, Min(0f)] private float shakeStamina = 20f;
        [SerializeField, Range(0f, 1f)] private float shakeChance = 0.3f;
        [Tooltip("How far a shake is heard by enemies, in world units.")]
        [SerializeField, Min(0f)] private float shakeNoise = 7f;
        [Tooltip("Failed shakes after which the machine jams.")]
        [SerializeField, Min(1)] private int jamAfter = 3;

        private readonly VendingStock contents = new VendingStock();
        private bool filled;
        private int coming;

        /// <summary>Raised when what is inside or what is coming out changes; the text says what happened, or is null.</summary>
        public event Action<string> Changed;

        public string Prompt => "Автомат с едой";
        public Vector2 Position => transform.position;
        public ItemDefinition Coin => coin;
        /// <summary>Things paid for (or shaken loose) that have not come out yet.</summary>
        public int Coming => coming;
        public bool CanShake => PlayerSkills.Has(PlayerSkills.VendingShake);

        public VendingStock Contents
        {
            get
            {
                if (!filled) Fill(new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
                return contents;
            }
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Rolls what is inside (done by itself on first look; for tests with given dice).</summary>
        public void Fill(System.Random dice)
        {
            contents.Fill(stock, Mathf.Min(minItems, maxItems), Mathf.Max(minItems, maxItems), emptyChance, dice);
            filled = true;
        }

        public bool CanInteract(GameObject actor) => !ModalState.IsOpen;

        public void Interact(GameObject actor) => VendingWindow.Open(actor, this);

        /// <summary>Pays a coin for the thing and sets it coming out. Null when done, else why not.</summary>
        public string Buy(GameObject actor, ItemDefinition item)
        {
            if (!actor.TryGetComponent<GridInventory>(out var backpack)) return "Не получится";
            if (!Contents.Slots.Any(s => s.Item == item)) return "Закончилось";
            if (VendingStock.Coins(backpack, coin) <= 0) return "Нужна монета";
            VendingStock.PayCoin(backpack, coin);
            Contents.Take(item);
            StartCoroutine(GiveOut(actor, item));
            return null;
        }

        /// <summary>One shake. What happened, for the window to say.</summary>
        public string Shake(GameObject actor)
        {
            if (!CanShake) return "Не знаю, как это делается";
            if (Contents.Jammed) return "Автомат заклинило";
            if (Contents.IsEmpty) return "Внутри пусто";
            if (actor.TryGetComponent<PlayerStats>(out var stats) && !stats.TrySpendStamina(shakeStamina)) return "Не хватает сил";

            Noise.Emit(transform.position, shakeNoise, actor);
            var result = Contents.Shake(UnityEngine.Random.value, UnityEngine.Random.value, shakeChance, jamAfter, out var dropped);
            switch (result)
            {
                case VendingStock.ShakeResult.Dropped:
                    StartCoroutine(GiveOut(actor, dropped));
                    Changed?.Invoke("Что-то сдвинулось внутри…");
                    return "Что-то сдвинулось внутри…";
                case VendingStock.ShakeResult.Jammed:
                    Changed?.Invoke("Автомат заклинило");
                    return "Автомат заклинило";
                default:
                    return "Ничего не выпало";
            }
        }

        // A thing comes out after a moment: to the player who waited for it, or onto the floor.
        private IEnumerator GiveOut(GameObject actor, ItemDefinition item)
        {
            coming++;
            Changed?.Invoke(null);
            yield return new WaitForSeconds(UnityEngine.Random.Range(Mathf.Min(dropDelay.x, dropDelay.y), Mathf.Max(dropDelay.x, dropDelay.y)));
            coming--;
            // The thing falls into the tray with a clatter, not as loud as shaking the machine.
            if (dropNoise > 0f) Noise.Emit(transform.position, dropNoise, gameObject);

            if (actor != null && Near(actor) && actor.TryGetComponent<GridInventory>(out var backpack) && backpack.Add(item, 1) == 0)
            {
                Tell($"Выпало: {item.DisplayName}");
                yield break;
            }
            DropInFront(item, actor);
            Tell($"Выпало на пол: {item.DisplayName}");
        }

        // To the open window of the machine; with the window closed, a note on the screen.
        private void Tell(string text)
        {
            if (Changed != null) Changed.Invoke(text);
            else ScreenNote.Show(text, 3f);
        }

        private bool Near(GameObject actor)
        {
            Vector2 here = transform.position;
            Vector2 there = actor.transform.position;
            var wrap = WrapWorld.Active;
            Vector2 delta = wrap != null ? wrap.Delta(here, there) : there - here;
            return delta.magnitude <= reach;
        }

        // In front of the tray (the bottom of the picture); where that is in a wall or furniture, on the player's side.
        private void DropInFront(ItemDefinition item, GameObject actor)
        {
            if (item == null || item.WorldPrefab == null) return;
            int blocked = LayerMask.GetMask("Obstacle", "Furniture", "Window");
            const float distance = 1.4f;
            Vector2 here = transform.position;
            Vector2 spot = here - (Vector2)transform.up * distance;
            if (Physics2D.OverlapCircle(spot, 0.25f, blocked) != null && actor != null)
            {
                var wrap = WrapWorld.Active;
                Vector2 toward = wrap != null ? wrap.Delta(here, actor.transform.position) : (Vector2)actor.transform.position - here;
                if (toward.sqrMagnitude > 0.0001f) spot = here + toward.normalized * distance;
            }
            var dropped = Instantiate(item.WorldPrefab, spot, Quaternion.identity);
            if (dropped.TryGetComponent<ItemPickup>(out var pickup)) pickup.Set(item, 1);
        }
    }
}
