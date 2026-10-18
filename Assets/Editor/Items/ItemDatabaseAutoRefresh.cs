using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Items;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Keeps Resources/ItemDatabase up to date by itself. When an item asset appears (made, duplicated with Ctrl+D,
    /// copied in), is deleted, moved or renamed, the database is rebuilt the way Mayham > Refresh Item Database does
    /// it: once, after the import is over, and only when the list of items has really changed, so the file does not
    /// show up in git for nothing. Without a database (a project that never made one) nothing happens.
    /// </summary>
    public class ItemDatabaseAutoRefresh : AssetPostprocessor
    {
        private const string DatabasePath = "Assets/Resources/ItemDatabase.asset";

        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (scheduled) return;
            // A deleted asset cannot be asked for its type any more: any deleted .asset is a reason to look.
            bool items = imported.Concat(moved).Any(IsItem) || deleted.Any(path => path.EndsWith(".asset"));
            if (!items) return;

            Schedule();
        }

        // On the next tick of the Editor. Not delayCall: that one waits for an Inspector to repaint, which an Editor
        // in the background (or without an Inspector open) never does.
        private static void Schedule()
        {
            scheduled = true;
            EditorApplication.update -= Refresh;
            EditorApplication.update += Refresh;
        }

        private static bool IsItem(string path) =>
            path.EndsWith(".asset") && typeof(ItemDefinition).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path));

        private static void Refresh()
        {
            // Still importing: stay on the next ticks until it is over.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Refresh;
            scheduled = false;

            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (database == null) return;

            var before = database.Items.ToList();
            var now = ProjectItems();
            if (before.SequenceEqual(now)) return;

            ShelterSetup.RefreshItemDatabase();
            var added = now.Where(item => !before.Contains(item)).Select(item => item.Id).ToList();
            int removed = before.Count(item => item == null || !now.Contains(item));
            Debug.Log("ItemDatabase обновлена сама: " +
                (added.Count > 0 ? "добавлено " + string.Join(", ", added) : "новых предметов нет") +
                (removed > 0 ? $", убрано {removed}" : string.Empty) + ".", database);
        }

        /// <summary>Every item asset of the project, in the order the database keeps them (by id).</summary>
        public static List<ItemDefinition> ProjectItems() =>
            AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .OrderBy(item => item.Id)
                .ToList();
    }
}
