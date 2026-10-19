using ProjectMayham.Level;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Puts the things the needs use into the prefabs: a toilet (<see cref="Toilet"/>), a water cooler and a tap
    /// (<see cref="WaterPoint"/>). The rooms hold these prefabs as nested instances, so every room has them after
    /// this. Mayham > Needs > 2. Set Up Fixtures. Safe to run again; the numbers of a tuned fixture are kept.
    /// </summary>
    public static class NeedsSetup
    {
        public const string CoolerPrefab = "Assets/Prefabs/Interactable/Water-Dispenser.prefab";
        public const string TapPrefab = "Assets/Prefabs/Furniture/WC-Sink.prefab";
        public static readonly string[] ToiletPrefabs =
        {
            "Assets/Prefabs/Furniture/Toilet-Closed.prefab",
            "Assets/Prefabs/Furniture/Toilet-Open.prefab",
        };

        [MenuItem("Mayham/Needs/2. Set Up Fixtures")]
        public static void SetUpMenu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            var report = new System.Collections.Generic.List<string>();
            foreach (string path in ToiletPrefabs) Ensure<Toilet>(path, null, report);

            Ensure<WaterPoint>(CoolerPrefab, null, report);
            // A tap gives less than a cooler and takes longer.
            Ensure<WaterPoint>(TapPrefab, so =>
            {
                so.FindProperty("prompt").stringValue = "Попить из-под крана (удерживать)";
                so.FindProperty("seconds").floatValue = 3f;
                so.FindProperty("hydration").floatValue = 0.2f;
                so.FindProperty("natureCall").floatValue = 0.05f;
            }, report);

            AssetDatabase.SaveAssets();
            return "Needs fixtures: " + string.Join(", ", report);
        }

        private static void Ensure<T>(string path, System.Action<SerializedObject> configure, System.Collections.Generic.List<string> report)
            where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                report.Add($"{path}: MISSING");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<T>() != null)
                {
                    report.Add($"{root.name}: already has {typeof(T).Name}");
                    return;
                }

                var component = root.AddComponent<T>();
                if (configure != null)
                {
                    var so = new SerializedObject(component);
                    configure(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add($"{root.name}: {typeof(T).Name} added");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
