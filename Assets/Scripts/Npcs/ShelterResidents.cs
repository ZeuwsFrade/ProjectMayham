using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Dialogue;
using ProjectMayham.Items;
using ProjectMayham.Quests;
using ProjectMayham.Shelter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMayham.Npcs
{
    /// <summary>
    /// Put into every shelter scene (one with a stash) when it loads, so no shelter scene has to be changed for it.
    /// After the player's things are loaded it takes out the NPCs the player carried home, marks them rescued and
    /// hands in their rescue quests (the NPC then thanks the player), then puts every rescued NPC at their
    /// <see cref="NpcSpot"/>. A shelter without such a spot gets none of them, with a warning in the console.
    /// The blueprints the player brought home are learned here too (<see cref="Blueprints.LearnCarried"/>).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ShelterResidents : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            // The scene the game started in is already loaded.
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.isLoaded || FindFirstObjectByType<StashChest>() == null || FindFirstObjectByType<ShelterResidents>() != null) return;
            var go = new GameObject("Residents");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<ShelterResidents>();
        }

        // After PlayerPersistence (order -50) has put the saved things into the player's inventory.
        private void Start()
        {
            var persistence = FindFirstObjectByType<PlayerPersistence>();
            var inventory = persistence != null ? persistence.GetComponent<GridInventory>() : null;
            var outcomes = new List<(NpcDefinition npc, QuestDefinition quest, TurnInResult result)>();
            foreach (var npc in NpcRescue.TakeCarried(inventory))
            {
                NpcRescue.Rescue(npc.Id);
                foreach (var (quest, result) in NpcRescue.FinishRescue(npc.Id, inventory)) outcomes.Add((npc, quest, result));
            }

            // Blueprints found in the raid are learned at home.
            var learned = Blueprints.LearnCarried(inventory);
            if (learned.Count > 0) StartCoroutine(Announce(learned));

            var spots = FindObjectsByType<NpcSpot>(FindObjectsSortMode.None).OrderBy(s => s.name).ToList();
            var taken = new HashSet<NpcSpot>();
            foreach (var npc in NpcCatalog.All.Where(n => NpcRescue.IsInShelter(n.Id) && n.ShelterPrefab != null).OrderBy(n => n.Id))
            {
                var spot = spots.FirstOrDefault(s => s.NpcId == npc.Id && !taken.Contains(s))
                    ?? spots.FirstOrDefault(s => s.NpcId.Length == 0 && !taken.Contains(s));
                if (spot == null)
                {
                    Debug.LogWarning($"ShelterResidents: {npc.Id} lives in the shelter, but this scene has no NpcSpot for them.", this);
                    continue;
                }
                taken.Add(spot);
                var resident = Instantiate(npc.ShelterPrefab, spot.transform.position, Quaternion.identity);
                resident.name = npc.Id;
            }

            if (outcomes.Count > 0) StartCoroutine(Thank(outcomes[0]));
        }

        private static IEnumerator Announce(List<ShelterModuleDefinition> learned)
        {
            while (SceneLoader.IsLoading) yield return null;
            yield return null;
            string names = string.Join(", ", learned.Select(m => $"«{m.DisplayName}»"));
            UI.ScreenNote.Show((learned.Count == 1 ? "Изучен чертёж: " : "Изучены чертежи: ") + names, 6f);
        }

        // The rescued NPC thanks the player once the shelter is on screen.
        private IEnumerator Thank((NpcDefinition npc, QuestDefinition quest, TurnInResult result) outcome)
        {
            while (SceneLoader.IsLoading) yield return null;
            yield return null;
            var portrait = outcome.npc.Portrait;
            DialogueWindow.Open(outcome.npc.DisplayName, portrait, QuestDialogue.Outcome(outcome.quest, outcome.result));
        }
    }
}
