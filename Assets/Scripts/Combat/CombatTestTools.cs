using System.Collections;
using System.Collections.Generic;
using ProjectMayham.AI;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.Vision;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// Keys and an on-screen log for the combat test scene: spawn packs, give and repair weapons, clear the field,
    /// switch the fog of war, and see what every blow did. Test scene only; nothing in the game depends on it.
    /// </summary>
    public class CombatTestTools : MonoBehaviour
    {
        private const int LogLines = 9;

        [Tooltip("Handed to the player when the scene starts and on F2. The first one is taken in the hands.")]
        [SerializeField] private WeaponDefinition[] weapons;
        [SerializeField] private EnemySpawner spawner;
        [Tooltip("Enemies of a pack made with F1.")]
        [SerializeField, Min(1)] private int packSize = 4;
        [SerializeField] private bool giveWeaponsOnStart = true;

        private readonly List<string> log = new List<string>();
        private PlayerStats stats;
        private GridInventory inventory;
        private MeleeCombat melee;
        private GUIStyle style;
        private AlertLevel alert;

        private void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>();
        }

        private void OnEnable()
        {
            CombatEvents.HitLanded += OnHit;
            CombatEvents.WeaponBroken += OnBroken;
            CombatEvents.ActorDied += OnDied;
            CombatEvents.AlertChanged += OnAlert;
            CombatEvents.EnemyShouted += OnShout;
            CombatEvents.SwingMissed += OnMiss;
        }

        private void OnDisable()
        {
            CombatEvents.HitLanded -= OnHit;
            CombatEvents.WeaponBroken -= OnBroken;
            CombatEvents.ActorDied -= OnDied;
            CombatEvents.AlertChanged -= OnAlert;
            CombatEvents.EnemyShouted -= OnShout;
            CombatEvents.SwingMissed -= OnMiss;
        }

        private IEnumerator Start()
        {
            // After the player has loaded their saved inventory.
            yield return null;
            Resolve();
            if (giveWeaponsOnStart) GiveWeapons();
        }

        private void Resolve()
        {
            if (stats != null) return;
            stats = FindFirstObjectByType<PlayerStats>();
            if (stats == null) return;
            inventory = stats.GetComponent<GridInventory>();
            melee = stats.GetComponent<MeleeCombat>();
        }

        private void Update()
        {
            Resolve();
            var keyboard = Keyboard.current;
            if (keyboard == null || stats == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) SpawnPackAhead();
            if (keyboard.f2Key.wasPressedThisFrame) GiveWeapons();
            if (keyboard.f3Key.wasPressedThisFrame) { stats.RestoreAll(); Add("игрок: здоровье и выносливость восстановлены"); }
            if (keyboard.f4Key.wasPressedThisFrame) ClearEnemies();
            if (keyboard.f5Key.wasPressedThisFrame) Repair();
            if (keyboard.f6Key.wasPressedThisFrame) ToggleVision();
            if (keyboard.rKey.wasPressedThisFrame && keyboard.leftCtrlKey.isPressed) SceneManager.LoadScene(gameObject.scene.name);
        }

        // ---- actions ----

        private void GiveWeapons()
        {
            if (inventory == null || weapons == null) return;

            foreach (var weapon in weapons)
            {
                if (weapon != null && inventory.CountOf(weapon) == 0 && inventory.HeldItem != weapon) inventory.Add(weapon, 1);
            }
            if (inventory.HeldItem == null && weapons.Length > 0 && weapons[0] != null)
            {
                foreach (var stack in inventory.Stacks)
                {
                    if (stack.Item != weapons[0]) continue;
                    inventory.Hold(stack);
                    break;
                }
            }
            Add("оружие выдано");
        }

        private void SpawnPackAhead()
        {
            if (spawner == null) return;

            // Ten units in the direction the player looks; the spawner puts the pack on free ground near it.
            Vector2 at = (Vector2)stats.transform.position + (Vector2)stats.transform.up * 10f;
            int made = spawner.SpawnPack(at, packSize);
            Add($"стая: {made} доходяг");
        }

        private void ClearEnemies()
        {
            int count = 0;
            foreach (var enemy in new List<EnemyAI>(EnemyAI.All))
            {
                Destroy(enemy.gameObject);
                count++;
            }
            Add($"убрано врагов: {count}");
        }

        private void Repair()
        {
            if (weapons != null)
            {
                foreach (var weapon in weapons) WeaponWear.Forget(weapon);
            }
            foreach (var health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                // Dummies (a Health with no mind next to it) get up again.
                if (health.GetComponent<EnemyAI>() == null) health.Revive();
            }
            Add("оружие починено, манекены целы");
        }

        private void ToggleVision()
        {
            var cone = FindFirstObjectByType<VisionCone>();
            var renderer = FindFirstObjectByType<VisionRenderer>();
            if (cone == null) return;

            bool on = !cone.enabled;
            cone.enabled = on;
            if (renderer != null) renderer.enabled = on;
            Add(on ? "туман войны включён" : "туман войны выключен");
        }

        // ---- log ----

        private void Add(string line)
        {
            log.Add($"{Time.time:0.0}  {line}");
            if (log.Count > LogLines) log.RemoveAt(0);
        }

        private static string Name(GameObject go) => go != null ? go.name.Replace("(Clone)", string.Empty) : "?";

        private void OnHit(HitEvent e)
        {
            string weapon = e.Weapon != null ? e.Weapon.DisplayName : "когти";
            string kind = e.Heavy ? "тяжёлый" : "удар";
            Add($"{Name(e.Attacker)} → {Name((e.Target as Component)?.gameObject)}: {weapon}, {kind}, {e.Damage:0}{(e.Killed ? "  УБИТ" : string.Empty)}");
        }

        private void OnMiss(SwingEvent e) => Add($"{Name(e.Attacker)}: мимо");
        private void OnBroken(WeaponDefinition weapon) => Add($"сломалось: {weapon.DisplayName}");
        private void OnDied(GameObject actor) => Add($"погиб: {Name(actor)}");
        private void OnShout(GameObject enemy, Vector2 at) => Add($"{Name(enemy)} кричит, зовёт стаю");
        private void OnAlert(AlertLevel level)
        {
            alert = level;
            Add($"тревога: {level}");
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { richText = true };
                style.normal.textColor = Color.white;
            }
            float scale = Screen.height / 1080f;
            style.fontSize = Mathf.RoundToInt(20f * scale);

            string state = stats == null ? string.Empty
                : $"здоровье {stats.Health:0}/{stats.MaxHealth:0}   выносливость {stats.Stamina:0}   врагов {EnemyAI.All.Count}   тревога {alert}"
                  + (melee != null ? $"   фаза {melee.Phase}" : string.Empty);
            string help = "F1 стая впереди · F2 оружие · F3 вылечить · F4 убрать врагов · F5 починить · F6 туман · Ctrl+R перезапуск";

            var rect = new Rect(24f * scale, 130f * scale, Screen.width * 0.7f, 400f * scale);
            GUI.Label(rect, $"<color=#ffd24d>{help}</color>\n{state}\n\n{string.Join("\n", log)}", style);
        }
    }
}
