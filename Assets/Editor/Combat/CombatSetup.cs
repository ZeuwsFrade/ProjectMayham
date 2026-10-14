using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMayham.AI;
using ProjectMayham.Combat;
using ProjectMayham.Items;
using ProjectMayham.Player;
using ProjectMayham.Trade;
using ProjectMayham.UI;
using ProjectMayham.Vision;
using ProjectMayham.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// One-click setup of the fighting: placeholder art, the two test weapons, the Dokhodyaga with its prefab, and the
    /// wiring of the raid scene (combat on the player, camera shake, HUD, navigation grid, enemy spawner).
    /// Every step can be run again; what exists is updated, not duplicated.
    /// </summary>
    public static class CombatSetup
    {
        private const string SpriteDir = "Assets/Sprites/Combat";
        private const string WeaponDir = "Assets/Data/Items/Weapons";
        private const string EnemyDir = "Assets/Data/Enemies";
        private const string WeaponPrefabDir = "Assets/Prefabs/Weapons";
        private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/Dokhodyaga.prefab";
        private const string PickupTemplate = "Assets/Prefabs/Interactable/Mop.prefab";
        private const string LitMaterialPrefab = "Assets/Prefabs/TestEnemy.prefab";
        private const string TraderPath = "Assets/Data/Traders/Tyler.asset";

        [MenuItem("Mayham/Combat/Run all steps")]
        public static void RunAll()
        {
            CreateAssets();
            WireOpenScene();
        }

        // ================= assets =================

        [MenuItem("Mayham/Combat/1. Weapons, enemy and prefabs")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(WeaponDir);
            Directory.CreateDirectory(EnemyDir);
            Directory.CreateDirectory(WeaponPrefabDir);
            Directory.CreateDirectory(Path.GetDirectoryName(EnemyPrefabPath));

            var pipeSprite = MakeSprite(SpriteDir + "/Pipe.png", 5, 24, DrawPipe);
            var hammerSprite = MakeSprite(SpriteDir + "/Sledgehammer.png", 10, 28, DrawHammer);
            var enemySprite = MakeSprite(SpriteDir + "/Dokhodyaga.png", 16, 16, DrawDokhodyaga);

            // Light and fast, against heavy and slow.
            var pipe = EnsureWeapon("Pipe", "Труба", pipeSprite, new Vector2Int(1, 3), 2.5f, 15, new Dictionary<string, object>
            {
                { "damage", 18f }, { "heavyDamageMultiplier", 2f },
                { "range", 1.55f }, { "arc", 95f }, { "heavyArcMultiplier", 1.15f },
                { "swingSeconds", 0.18f }, { "heavySwingSeconds", 0.28f },
                { "recoverSeconds", 0.12f }, { "heavyRecoverSeconds", 0.3f }, { "chargeSeconds", 0.55f },
                { "staminaCost", 8f }, { "heavyStaminaCost", 20f },
                { "knockback", 3.5f }, { "heavyKnockbackMultiplier", 1.8f },
                { "stagger", 0.3f }, { "heavyStagger", 0.8f },
                { "hitStop", 0.035f }, { "heavyHitStop", 0.08f },
                { "shake", 0.18f }, { "heavyShake", 0.5f },
                { "raisedSpeedMultiplier", 0.65f }, { "swingSpeedMultiplier", 0.35f }, { "raisedConeScale", 0.65f },
                { "maxDurability", 70f }, { "durabilityPerHit", 1f }, { "heavyWearMultiplier", 2f },
                { "swingNoise", 3.5f }, { "hitNoise", 7f }
            });
            var hammer = EnsureWeapon("Sledgehammer", "Кувалда", hammerSprite, new Vector2Int(2, 3), 6f, 40, new Dictionary<string, object>
            {
                { "damage", 34f }, { "heavyDamageMultiplier", 2f },
                { "range", 1.85f }, { "arc", 85f }, { "heavyArcMultiplier", 1.2f },
                { "swingSeconds", 0.34f }, { "heavySwingSeconds", 0.5f },
                { "recoverSeconds", 0.3f }, { "heavyRecoverSeconds", 0.6f }, { "chargeSeconds", 0.9f },
                { "staminaCost", 18f }, { "heavyStaminaCost", 38f },
                { "knockback", 6f }, { "heavyKnockbackMultiplier", 1.8f },
                { "stagger", 0.6f }, { "heavyStagger", 1.4f },
                { "hitStop", 0.07f }, { "heavyHitStop", 0.14f },
                { "shake", 0.4f }, { "heavyShake", 0.8f },
                { "raisedSpeedMultiplier", 0.5f }, { "swingSpeedMultiplier", 0.2f }, { "raisedConeScale", 0.5f },
                { "maxDurability", 45f }, { "durabilityPerHit", 1f }, { "heavyWearMultiplier", 2f },
                { "swingNoise", 5f }, { "hitNoise", 10f }
            });

            EnsureDokhodyaga(enemySprite);

            ShelterSetup.RefreshItemDatabase();
            AddToTrader(pipe, 1);
            AddToTrader(hammer, 1);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CombatSetup: weapons Pipe and Sledgehammer, enemy Dokhodyaga and their prefabs are ready.");
        }

        private static WeaponDefinition EnsureWeapon(string id, string title, Sprite sprite, Vector2Int grid, float weight, int value,
            Dictionary<string, object> numbers)
        {
            string itemPath = $"{WeaponDir}/{id}.asset";
            string prefabPath = $"{WeaponPrefabDir}/{id}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) AssetDatabase.CopyAsset(PickupTemplate, prefabPath);

            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(itemPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, itemPath);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var values = new Dictionary<string, object>(numbers)
            {
                { "displayName", title }, { "icon", sprite }, { "maxStack", 1 }, { "gridSize", grid }, { "canRotate", true },
                { "weight", weight }, { "value", value }, { "holdable", true },
                { "heldOffset", new Vector2(0.45f, 0.55f) }, { "heldAngle", 0f }, { "heldMatchesWorld", true },
                { "worldPrefab", prefab }
            };
            Apply(weapon, values);

            // The pickup in the world: the template's picture and item replaced.
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            contents.name = id;
            contents.transform.localScale = Vector3.one;
            contents.GetComponentInChildren<SpriteRenderer>().sprite = sprite;
            var pickup = contents.GetComponent<ItemPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("item").objectReferenceValue = weapon;
            so.FindProperty("count").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void EnsureDokhodyaga(Sprite sprite)
        {
            string path = $"{EnemyDir}/Dokhodyaga.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            // Frail and slow alone; the danger is the crowd.
            Apply(definition, new Dictionary<string, object>
            {
                { "displayName", "Доходяга" },
                { "maxHealth", 40f }, { "staggerResistance", 0f }, { "knockbackResistance", 0f },
                { "patrolSpeed", 0.8f }, { "investigateSpeed", 1.7f }, { "chaseSpeed", 2.7f },
                { "acceleration", 10f }, { "turnSpeed", 280f }, { "patrolRadius", 7f }, { "patrolPause", new Vector2(1.5f, 4f) },
                { "sightRange", 7f }, { "sightAngle", 110f }, { "senseRange", 2.2f }, { "hearing", 1f },
                { "noticeSeconds", 1f }, { "forgetSeconds", 8f },
                { "loseTargetSeconds", 4f }, { "searchSeconds", 4f },
                { "attackRange", 1.15f }, { "attackArc", 110f }, { "damage", 6f }, { "attackKnockback", 2.5f },
                { "windupSeconds", 0.65f }, { "recoverSeconds", 0.5f }, { "cooldownSeconds", 1f }, { "lunge", 0.35f },
                { "shouts", true }, { "shoutRadius", 14f }, { "surroundRange", 5f }, { "separationRadius", 0.9f }
            });
            EditorUtility.SetDirty(definition);

            // The prefab is rebuilt from scratch every time, so a change to the recipe reaches it.
            var litMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(LitMaterialPrefab).GetComponent<SpriteRenderer>().sharedMaterial;

            var root = new GameObject("Dokhodyaga") { layer = LayerMask.NameToLayer("Enemy") };
            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 1.2f;
            var circle = root.AddComponent<CircleCollider2D>();
            circle.radius = 0.34f;

            var body = new GameObject("Body") { layer = root.layer };
            body.transform.SetParent(root.transform, false);
            var renderer = body.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = litMaterial;
            renderer.sortingOrder = 8;

            root.AddComponent<Health>();
            root.AddComponent<EnemyMotor>();
            var ai = root.AddComponent<Dokhodyaga>();
            root.AddComponent<HitFlash>();
            root.AddComponent<VisionTarget>();
            root.AddComponent<WrapEntity>();

            var so = new SerializedObject(ai);
            so.FindProperty("definition").objectReferenceValue = definition;
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
        }

        // The traders' stock: what is new is added, what is there is left.
        private static void AddToTrader(ItemDefinition item, int count)
        {
            var trader = AssetDatabase.LoadAssetAtPath<TraderDefinition>(TraderPath);
            if (trader == null) return;

            var so = new SerializedObject(trader);
            var stock = so.FindProperty("stock");
            for (int i = 0; i < stock.arraySize; i++)
            {
                if (stock.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) return;
            }

            stock.arraySize++;
            var entry = stock.GetArrayElementAtIndex(stock.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("count").intValue = count;
            entry.FindPropertyRelative("priceOverride").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(trader);
        }

        // ================= the scene =================

        [MenuItem("Mayham/Combat/2. Wire the open raid scene")]
        public static void WireOpenScene() => Wire(true);

        /// <summary>
        /// The same wiring, but the scene is left unsaved: to try the fighting out in a scene that is somebody else's
        /// (close it without saving afterwards).
        /// </summary>
        [MenuItem("Mayham/Combat/2b. Wire the open scene without saving (try it out)")]
        public static void WireOpenSceneUnsaved() => Wire(false);

        private static void Wire(bool save)
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("CombatSetup: there is no player in the open scene.");
                return;
            }

            WirePlayer(player.gameObject);

            var camera = Camera.main;
            if (camera != null && camera.GetComponent<CameraShake>() == null) camera.gameObject.AddComponent<CameraShake>();

            var hud = Object.FindFirstObjectByType<MoneyHUD>();
            if (hud != null && hud.GetComponent<CombatHUD>() == null) hud.gameObject.AddComponent<CombatHUD>();

            EnsureNavGrid();
            EnsureSpawner();

            if (!save)
            {
                Debug.Log("CombatSetup: combat is wired into " + player.gameObject.scene.name + " (not saved)");
                return;
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("CombatSetup: combat is wired into " + player.gameObject.scene.name);
        }

        private static void WirePlayer(GameObject player)
        {
            var controller = player.GetComponent<PlayerController>();
            var actions = new SerializedObject(controller).FindProperty("inputActions").objectReferenceValue;

            if (player.GetComponent<MeleeCombat>() == null)
            {
                var combat = player.AddComponent<MeleeCombat>();
                var so = new SerializedObject(combat);
                so.FindProperty("inputActions").objectReferenceValue = actions;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (player.GetComponent<PlayerNoise>() == null) player.AddComponent<PlayerNoise>();
            if (player.GetComponent<PlayerDeath>() == null) player.AddComponent<PlayerDeath>();
            if (player.GetComponent<HitFlash>() == null) player.AddComponent<HitFlash>();
        }

        private static void EnsureNavGrid()
        {
            var grid = Object.FindFirstObjectByType<NavGrid>();
            if (grid == null) grid = new GameObject("NavGrid").AddComponent<NavGrid>();

            var so = new SerializedObject(grid);
            so.FindProperty("obstacleMask").intValue = LayerMask.GetMask("Obstacle", "Furniture", "Window");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSpawner()
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();

            var parent = GameObject.Find("Raid Enemies");
            if (parent == null) parent = new GameObject("Raid Enemies");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var so = new SerializedObject(spawner);
            var list = so.FindProperty("prefabs");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
            so.FindProperty("parent").objectReferenceValue = parent.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================= test helpers (play mode) =================

        [MenuItem("Mayham/Combat/Give test weapons (play mode)")]
        public static void GiveTestWeapons()
        {
            // The player's own: loot containers have inventories too.
            var controller = Object.FindFirstObjectByType<PlayerController>();
            var player = controller != null ? controller.GetComponent<GridInventory>() : null;
            if (player == null || !Application.isPlaying)
            {
                Debug.LogWarning("CombatSetup: start the game first.");
                return;
            }

            foreach (var id in new[] { "Pipe", "Sledgehammer" })
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponDir}/{id}.asset");
                if (weapon != null) player.Add(weapon, 1);
            }
            var pipe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponDir}/Pipe.asset");
            var stack = player.Stacks.FirstOrDefault(s => s.Item == pipe);
            if (stack != null) player.Hold(stack);
        }

        [MenuItem("Mayham/Combat/Spawn a pack near the player (play mode)")]
        public static void SpawnPackNearPlayer()
        {
            var player = Object.FindFirstObjectByType<PlayerStats>();
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (player == null || spawner == null || !Application.isPlaying) return;

            var at = (Vector2)player.transform.position + (Vector2)player.transform.up * 8f;
            Debug.Log("CombatSetup: spawned " + spawner.SpawnPack(at, 4) + " enemies");
        }

        // ================= helpers =================

        private static void Apply(Object target, Dictionary<string, object> values)
        {
            var so = new SerializedObject(target);
            foreach (var pair in values)
            {
                var property = so.FindProperty(pair.Key);
                if (property == null)
                {
                    Debug.LogWarning($"CombatSetup: {target.GetType().Name} has no field {pair.Key}.");
                    continue;
                }
                switch (pair.Value)
                {
                    case float f: property.floatValue = f; break;
                    case int i: property.intValue = i; break;
                    case bool b: property.boolValue = b; break;
                    case string s: property.stringValue = s; break;
                    case Vector2 v: property.vector2Value = v; break;
                    case Vector2Int v: property.vector2IntValue = v; break;
                    case Object o: property.objectReferenceValue = o; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private delegate void Painter(Color[] pixels, int w, int h);

        private static Sprite MakeSprite(string path, int w, int h, Painter paint)
        {
            if (!File.Exists(path))
            {
                var pixels = new Color[w * h];
                paint(pixels, w, h);
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Rect(Color[] p, int w, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    p[y * w + x] = c;
        }

        // A length of pipe, lying up, with a dark end.
        private static void DrawPipe(Color[] p, int w, int h)
        {
            Rect(p, w, 1, 0, 3, h - 1, new Color(0.62f, 0.64f, 0.68f));
            Rect(p, w, 1, 0, 1, h - 1, new Color(0.8f, 0.82f, 0.86f));
            Rect(p, w, 3, 0, 3, h - 1, new Color(0.42f, 0.44f, 0.48f));
            Rect(p, w, 0, h - 3, 4, h - 1, new Color(0.35f, 0.37f, 0.4f));
        }

        // A handle with a heavy head on top.
        private static void DrawHammer(Color[] p, int w, int h)
        {
            Rect(p, w, 4, 0, 5, h - 6, new Color(0.5f, 0.33f, 0.18f));
            Rect(p, w, 4, 0, 4, h - 6, new Color(0.62f, 0.43f, 0.25f));
            Rect(p, w, 0, h - 8, w - 1, h - 1, new Color(0.34f, 0.35f, 0.38f));
            Rect(p, w, 0, h - 8, w - 1, h - 7, new Color(0.22f, 0.23f, 0.26f));
            Rect(p, w, 1, h - 3, w - 2, h - 2, new Color(0.5f, 0.52f, 0.56f));
        }

        // A hunched sickly figure seen from above, arms stretched forward (up).
        private static void DrawDokhodyaga(Color[] p, int w, int h)
        {
            var skin = new Color(0.55f, 0.62f, 0.5f);
            var shade = new Color(0.38f, 0.45f, 0.36f);
            var cloth = new Color(0.4f, 0.38f, 0.34f);

            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f - 1f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / 6.2f, dy = (y - cy) / 5.2f;
                    if (dx * dx + dy * dy <= 1f) p[y * w + x] = cloth;
                }
            }
            // The head.
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx, dy = y - (cy + 1f);
                    if (dx * dx + dy * dy <= 6f) p[y * w + x] = skin;
                }
            }
            Rect(p, w, 3, 10, 4, 14, shade); // left arm
            Rect(p, w, w - 5, 10, w - 4, 14, shade); // right arm
            Rect(p, w, 3, 14, 4, 15, skin);
            Rect(p, w, w - 5, 14, w - 4, 15, skin);
        }
    }
}
