using System.Linq;
using ProjectMayham.Items;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        [MenuItem("Mayham/Quests/Check Quest Tools (makes temporary assets)")]
        public static void RunToolsCheckMenu() => Debug.Log(RunToolsCheck());

        /// <summary>
        /// Checks the menu commands that make assets, on real assets with a name of their own, which are deleted
        /// again. Kept apart from <see cref="Run"/> because it writes to the project (and to the item database).
        /// </summary>
        public static string RunToolsCheck()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            const string name = "SelfTestQuestItem";
            var t = new Checks { Group = "tools" };
            try
            {
                var first = QuestMenus.CreateQuestItem(name, "Первый");
                t.Ok(first != null && first.Value == 0 && first.MaxStack == 1, "a quest item has no price and does not stack");
                t.Ok(first.WorldPrefab != null && first.WorldPrefab.GetComponent<ItemPickup>().Item == first, "its pickup hands out the item");
                t.Ok(ItemDatabase.Find(name) == first, "it is in the item database");
                t.Ok(QuestMenus.CreateQuestItem(name, "Ещё раз") == first, "the same name again gives the same item");

                // The writer renames the item (the name is its id) and leaves the pickup alone, then makes another one.
                string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(first), name + "Renamed");
                t.Eq(error, string.Empty, "the item can be renamed");
                var second = QuestMenus.CreateQuestItem(name, "Второй");
                t.Ok(second != null && second != first, "a new item is made under the old name");
                t.Ok(second.WorldPrefab != first.WorldPrefab, "the new item gets a pickup of its own");
                t.Ok(first.WorldPrefab.GetComponent<ItemPickup>().Item == first, "the first pickup still hands out the first item");
                t.Ok(second.WorldPrefab.GetComponent<ItemPickup>().Item == second, "the second pickup hands out the second item");
            }
            catch (System.Exception e)
            {
                t.Count++;
                t.Failures.Add($"[tools] exception: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                foreach (string path in AssetDatabase.FindAssets(name).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains(name)))
                    AssetDatabase.DeleteAsset(path);
                ShelterSetup.RefreshItemDatabase();
            }

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures);
        }
    }
}
