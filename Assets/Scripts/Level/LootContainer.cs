using System.Linq;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.UI;
using UnityEngine;

namespace ProjectMayham.Level
{
    /// <summary>
    /// A piece of furniture that can be searched: a bin, a cabinet, a crate. A raid puts things into it that the
    /// player has not found yet (sealed stacks: they take up their cells but cannot be taken). The first time its
    /// window is open the player looks it over for a moment; only then does it show what is inside, or that it is
    /// empty. Then the things are found one by one; what was found stays found, and the player may leave things here.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        // A storage never refuses a thing for its weight.
        private const float NoWeightLimit = 100000f;
        // For a container that was given no profile.
        private const float DefaultSeconds = 1f;
        private const float DefaultLook = 0.8f;
        private static readonly Vector2Int DefaultGrid = new Vector2Int(3, 3);

        [SerializeField] private LootContainerProfile profile;

        private GridInventory inventory;
        private bool opened;
        private bool lookedIn;
        private float lookTimer;
        private float searchTimer;

        public LootContainerProfile Profile => profile;
        public string Title => profile != null ? profile.Title : name;

        /// <summary>The grid of the container. Made on first use, so it also exists in a preview outside Play mode.</summary>
        public GridInventory Inventory
        {
            get
            {
                if (inventory != null) return inventory;

                if (!TryGetComponent(out inventory)) inventory = gameObject.AddComponent<GridInventory>();
                inventory.Configure(profile != null ? profile.GridSize : DefaultGrid, NoWeightLimit);
                return inventory;
            }
        }

        private float SecondsPerItem => profile != null ? profile.SecondsPerItem : DefaultSeconds;
        private float LookSeconds => profile != null ? profile.LookSeconds : DefaultLook;

        /// <summary>True when one piece of the item fits into the grid of the container. Safe to ask of a prefab.</summary>
        public bool Fits(ItemDefinition item)
        {
            if (profile != null) return profile.Fits(item);
            if (item == null) return false;
            var size = item.GridSize;
            return (size.x <= DefaultGrid.x && size.y <= DefaultGrid.y)
                || (item.CanRotate && size.y <= DefaultGrid.x && size.x <= DefaultGrid.y);
        }

        /// <summary>Things that are in the container and have not been found yet.</summary>
        public int HiddenCount => inventory != null ? inventory.Stacks.Count(s => s.Sealed) : 0;

        /// <summary>True once the player has looked into the container and found everything there was.</summary>
        public bool IsSearched => lookedIn && HiddenCount == 0;

        /// <summary>True during the first look into the container: what is inside does not show yet.</summary>
        public bool IsLookingIn => opened && !lookedIn;

        /// <summary>0..1: how far the first look has got.</summary>
        public float LookProgress => lookedIn || LookSeconds <= 0f ? 1f : Mathf.Clamp01(lookTimer / LookSeconds);

        /// <summary>0..1: how far the search for the next thing has got.</summary>
        public float SearchProgress => HiddenCount > 0 ? Mathf.Clamp01(searchTimer / SecondsPerItem) : 0f;

        /// <summary>The thing that will be found next, or null: the hidden stack that comes first in reading order.</summary>
        public GridInventory.Stack Next => inventory == null
            ? null
            : inventory.Stacks.Where(s => s.Sealed).OrderBy(s => s.Origin.y).ThenBy(s => s.Origin.x).FirstOrDefault();

        public string Prompt
        {
            get
            {
                // Before the first look nobody knows whether there is anything inside.
                if (!IsSearched) return $"Обыскать: {Title}";
                return Inventory.Stacks.Count > 0 ? $"Открыть: {Title}" : $"{Title} (пусто)";
            }
        }

        public Vector2 Position => transform.position;

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Someone who is climbing or crawling has no hands free for a search.</summary>
        public bool CanInteract(GameObject actor) =>
            !actor.TryGetComponent<PlayerController>(out var controller) || !controller.MovementLocked;

        public void Interact(GameObject actor) => SearchWindow.Open(actor, this);

        /// <summary>Empties the container and puts in what its profile rolls for this raid, all of it not found yet.</summary>
        public int Fill(System.Random rng, LootTable roomLoot = null)
        {
            var grid = Inventory;
            grid.Clear();
            opened = false;
            lookedIn = false;
            lookTimer = 0f;
            searchTimer = 0f;
            if (profile == null) return 0;

            int placed = 0;
            foreach (var (item, count) in profile.Roll(rng, roomLoot))
            {
                // What does not fit (a mop in a bin) is simply not there.
                if (grid.AddSealed(item, count) != null) placed++;
            }
            return placed;
        }

        /// <summary>True when <see cref="Hide"/> would find room for this many pieces.</summary>
        public bool CanHide(ItemDefinition item, int count) => Inventory.CanFitApart(item, count);

        /// <summary>Puts a given thing into the container as something still to be found. False when it does not fit.</summary>
        public bool Hide(ItemDefinition item, int count)
        {
            if (!CanHide(item, count)) return false;

            int left = count;
            while (left > 0)
            {
                var stack = Inventory.AddSealed(item, left);
                if (stack == null) break;
                left -= stack.Count;
            }
            return left <= 0;
        }

        /// <summary>
        /// The player opens the container: the first time this starts the look into it (<see cref="Search"/> runs
        /// it); afterwards the prompt tells whether anything is left.
        /// </summary>
        public void Open()
        {
            opened = true;
            if (LookSeconds <= 0f) lookedIn = true;
        }

        /// <summary>Goes on searching for <paramref name="deltaTime"/> seconds. Returns how many things were found.</summary>
        public int Search(float deltaTime)
        {
            // The first look: what is inside, and whether anything is, shows only after it.
            if (!lookedIn)
            {
                if (!opened) return 0;
                lookTimer += Mathf.Max(0f, deltaTime);
                if (lookTimer < LookSeconds) return 0;
                lookedIn = true;
                deltaTime = lookTimer - LookSeconds;
            }

            if (HiddenCount == 0)
            {
                searchTimer = 0f;
                return 0;
            }

            searchTimer += Mathf.Max(0f, deltaTime);
            int found = 0;
            while (searchTimer >= SecondsPerItem)
            {
                var next = Next;
                if (next == null) break;
                searchTimer -= SecondsPerItem;
                inventory.Unseal(next);
                found++;
            }
            if (HiddenCount == 0) searchTimer = 0f;
            return found;
        }
    }
}
