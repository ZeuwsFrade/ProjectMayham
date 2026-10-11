using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class ContainerSelfTest
    {
        private const int RaidSeeds = 8;

        [MenuItem("Mayham/Level/Check Containers in Raids (open raid scene)")]
        public static void RunRaidCheckMenu() => Debug.Log(RunRaidCheck());

        /// <summary>
        /// Generates raids in the open scene and checks what is in their containers: without demands, with a quest
        /// item that has to be hidden in a container, with one that lies on the floor and with one too large for
        /// any container. Needs a scene with a RaidGenerator (TileTest) outside Play mode. The preview is cleared
        /// afterwards and the scene is not saved.
        /// </summary>
        public static string RunRaidCheck()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";
            var generator = Object.FindFirstObjectByType<RaidGenerator>();
            if (generator == null) return "FAIL: the open scene has no RaidGenerator (open Assets/Scenes/Tests/TileTest.unity)";

            var folder = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Quest/RedFolder.asset");
            if (folder == null) return "FAIL: no item at Assets/Data/Items/Quest/RedFolder.asset";

            // A thing no container is large enough for.
            var huge = ScriptableObject.CreateInstance<ItemDefinition>();
            huge.name = "SelfTestHuge";
            huge.hideFlags = HideFlags.HideAndDontSave;
            var so = new SerializedObject(huge);
            so.FindProperty("gridSize").vector2IntValue = new Vector2Int(9, 9);
            so.FindProperty("worldPrefab").objectReferenceValue = folder.WorldPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            var t = new QuestSelfTest.Checks { Group = "raid" };
            // Every raid prints its seed, and the thing that fits nowhere is warned about on purpose.
            var filter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error;
            try
            {
                int total = 0, withLoot = 0;
                var signatures = new HashSet<string>();
                for (int n = 1; n <= RaidSeeds; n++)
                {
                    int seed = n * 7919 + 13;
                    generator.Generate(seed);
                    string plain = Signature(generator);
                    signatures.Add(plain);
                    int inRooms = generator.Rooms.Sum(r => r.Instance.GetComponentsInChildren<LootContainer>().Length);
                    t.Ok(inRooms > 0, $"seed {seed}: the rooms have containers");
                    t.Eq(generator.Containers.Count, inRooms, $"seed {seed}: the generator knows every one of them");
                    t.Eq(generator.Rooms.Sum(r => r.ContainerCount), inRooms, $"seed {seed}: and counts them room by room");
                    t.Ok(generator.Containers.All(c => c.Profile != null), $"seed {seed}: every container has a profile");
                    t.Ok(generator.Containers.All(c => c.Inventory.Stacks.All(s => s.Sealed)), $"seed {seed}: nothing in them is found yet");
                    t.Eq(generator.QuestContainers.Count, 0, $"seed {seed}: nothing is hidden without demands");
                    total += generator.Containers.Count;
                    withLoot += generator.Containers.Count(c => c.HiddenCount > 0);

                    generator.Generate(seed);
                    t.Eq(Signature(generator), plain, $"seed {seed}: the same seed fills the containers the same way");

                    var hide = new RaidDemands();
                    hide.AddItem(folder, 1, RoomKind.BossOffice, true);
                    generator.Generate(seed, hide);
                    var office = generator.Rooms.FirstOrDefault(r => r.Forced && r.Kind == RoomKind.BossOffice);
                    t.Eq(generator.QuestPickups.Count, 0, $"seed {seed}: a hidden item does not lie on the floor");
                    t.Eq(generator.QuestContainers.Count, 1, $"seed {seed}: it is in one container");
                    t.Eq(CountIn(generator, folder), 1, $"seed {seed}: one piece of it in the whole raid");
                    if (office != null && generator.QuestContainers.Count == 1)
                    {
                        var container = generator.QuestContainers[0];
                        t.Ok(container.transform.IsChildOf(office.Instance.transform), $"seed {seed}: the container stands in the forced boss office");
                        t.Ok(container.Inventory.Stacks.Any(s => s.Item == folder && s.Sealed && s.Count == 1), $"seed {seed}: the item waits there to be found");
                    }
                    string hidden = Signature(generator);
                    generator.Generate(seed, hide);
                    t.Eq(Signature(generator), hidden, $"seed {seed}: same seed and demands, same hiding place");

                    var floor = new RaidDemands();
                    floor.AddItem(folder, 1, RoomKind.BossOffice);
                    generator.Generate(seed, floor);
                    t.Eq(generator.QuestPickups.Count, 1, $"seed {seed}: an item that is not hidden lies on the floor");
                    t.Eq(generator.QuestContainers.Count, 0, $"seed {seed}: and no container was picked for it");
                    t.Eq(CountIn(generator, folder), 0, $"seed {seed}: nor does one hold it");

                    var tooBig = new RaidDemands();
                    tooBig.AddItem(huge, 1, RoomKind.BossOffice, true);
                    generator.Generate(seed, tooBig);
                    t.Eq(generator.QuestContainers.Count, 0, $"seed {seed}: a thing no container takes is not hidden");
                    t.Eq(generator.QuestPickups.Count, 1, $"seed {seed}: it is put on the floor instead");
                }

                float share = total > 0 ? withLoot / (float)total : 0f;
                t.Ok(share > 0.24f && share < 0.4f, $"about a third of the containers hold something (got {share:0.00} of {total})");
                t.Ok(signatures.Count > 1, "different seeds fill the containers differently");
            }
            finally
            {
                Debug.unityLogger.filterLogType = filter;
                generator.Clear();
                Object.DestroyImmediate(huge);
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures.Take(20));
        }

        private static string Signature(RaidGenerator generator) =>
            string.Join("|", generator.Containers.Select(c => Contents(c.Inventory)));

        private static int CountIn(RaidGenerator generator, ItemDefinition item) =>
            generator.Containers.Sum(c => c.Inventory.Stacks.Where(s => s.Item == item).Sum(s => s.Count));
    }
}
