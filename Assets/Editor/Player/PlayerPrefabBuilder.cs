using System.Collections.Generic;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Interaction;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Prefabs/Player.prefab</c> with every player mechanic in one place (movement, vision, interaction,
    /// inventory, stats, held item, climbing, persistence, equipment, melee, noise, death, hit flash) and swaps the
    /// hand-made Player of a scene for an instance of it. References to the old Player (camera, wrap world, HUD...)
    /// are re-pointed to the matching components of the instance.
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Player.prefab";
        public const string FootstepsTrackPath = "Assets/Audio/Player/Steps_Track.mp3";

        // Obstacle + Window: nothing can be used through walls and glass; Furniture is added for the landing spot.
        private const int ObstacleWindowMask = (1 << 6) | (1 << 10);
        private const int LandingMask = (1 << 6) | (1 << 9) | (1 << 10);

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/Shelter.unity",
            "Assets/Scenes/Tests/ShelterTest.unity",
            "Assets/Scenes/Tests/CombatTest.unity",
        };

        [MenuItem("Mayham/Player/1. Build Player prefab")]
        public static void BuildPrefab()
        {
            var scene = EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Additive);
            var source = FindPlayer(scene);
            if (source == null) { Debug.LogError("PlayerPrefabBuilder: no Player in " + Scenes[0]); return; }

            var temp = Object.Instantiate(source);
            temp.name = "Player";
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var controller = temp.GetComponent<PlayerController>();
            var inputActions = new SerializedObject(controller).FindProperty("inputActions").objectReferenceValue;

            Ensure<PlayerClimber>(temp, so =>
            {
                so.FindProperty("inputActions").objectReferenceValue = inputActions;
                so.FindProperty("landingBlockMask").intValue = LandingMask;
            });
            Ensure<PlayerEquipment>(temp, null);
            Ensure<PlayerPersistence>(temp, null);
            Ensure<MeleeCombat>(temp, null);
            Ensure<PlayerNoise>(temp, null);
            Ensure<PlayerFootsteps>(temp, SetFootsteps);
            Ensure<PlayerDeath>(temp, null);
            Ensure<HitFlash>(temp, null);

            var interactor = new SerializedObject(temp.GetComponent<PlayerInteractor>());
            interactor.FindProperty("blockMask").intValue = ObstacleWindowMask;
            interactor.FindProperty("reachBlockMask").intValue = ObstacleWindowMask;
            interactor.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerPrefabBuilder: " + PrefabPath + " built");
        }

        [MenuItem("Mayham/Player/2. Replace Player in all scenes")]
        public static void ReplaceInAllScenes()
        {
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (ReplaceInScene(scene)) EditorSceneManager.SaveScene(scene);
            }
        }

        public static bool ReplaceInScene(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var old = FindPlayer(scene);
            if (prefab == null || old == null) return false;
            if (PrefabUtility.GetCorrespondingObjectFromSource(old) == prefab) return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            instance.transform.localScale = old.transform.localScale;
            instance.transform.SetSiblingIndex(old.transform.GetSiblingIndex());

            var map = new Dictionary<Object, Object> { [old] = instance };
            foreach (var component in old.GetComponents<Component>())
            {
                var counterpart = instance.GetComponent(component.GetType());
                if (counterpart != null) map[component] = counterpart;
            }

            int repointed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == old || root == instance) continue;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = it.objectReferenceValue;
                        if (value != null && map.TryGetValue(value, out var replacement))
                        {
                            it.objectReferenceValue = replacement;
                            changed = true;
                            repointed++;
                        }
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Object.DestroyImmediate(old);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"PlayerPrefabBuilder: {scene.name}: Player replaced, {repointed} references re-pointed");
            return true;
        }

        private static GameObject FindPlayer(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<PlayerController>(true);
                if (controller != null) return controller.gameObject;
            }
            return null;
        }

        /// <summary>
        /// The track of <see cref="FootstepsTrackPath"/> with its steps found, when the component has no sound yet
        /// (or always, with <paramref name="replace"/>).
        /// </summary>
        public static void SetFootsteps(SerializedObject footsteps) => SetFootsteps(footsteps, false);

        public static void SetFootsteps(SerializedObject footsteps, bool replace)
        {
            var track = footsteps.FindProperty("track");
            var steps = footsteps.FindProperty("steps");
            if (!replace && (track.objectReferenceValue != null || steps.arraySize > 0)) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepsTrackPath);
            if (clip == null) return;
            footsteps.FindProperty("kind").enumValueIndex = (int)PlayerFootsteps.SoundKind.Track;
            track.objectReferenceValue = clip;
            steps.arraySize = 0;
            // The same recording for the loop, so switching to "the whole track" plays at once.
            var loop = footsteps.FindProperty("loopTrack");
            if (loop.objectReferenceValue == null) loop.objectReferenceValue = clip;
            FootstepTrackAnalyzer.Fill(footsteps);
        }

        private static void Ensure<T>(GameObject go, System.Action<SerializedObject> configure) where T : Component
        {
            // A missing component is not null for ?? in the Editor, so ask with TryGetComponent.
            if (!go.TryGetComponent<T>(out var component)) component = go.AddComponent<T>();
            if (configure == null) return;
            var so = new SerializedObject(component);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
