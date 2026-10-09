using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.Shelter;
using ProjectMayham.Trade;
using ProjectMayham.UI;
using ProjectMayham.Vision;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// One-click setup of the shelter: placeholder art, data assets (items, trader, item database), the Shelter
    /// scene (a copy of MainScene stripped of the maze and rebuilt as a room) and the raid-side hooks in MainScene.
    /// Every step can be run again.
    /// </summary>
    public static class ShelterSetup
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string ShelterScenePath = "Assets/Scenes/Shelter.unity";
        private const string SpriteDir = "Assets/Sprites/Shelter";
        private const string FurnitureDir = "Assets/Sprites/Office Furniture/";
        private const string TraderPath = "Assets/Data/Traders/Tyler.asset";

        [MenuItem("Mayham/Shelter/Run all steps")]
        public static void RunAll()
        {
            SetupData();
            BuildShelterScene();
            PatchRaidScene();
        }

        [MenuItem("Mayham/Refresh Item Database")]
        public static void RefreshItemDatabase()
        {
            Directory.CreateDirectory("Assets/Resources");
            const string path = "Assets/Resources/ItemDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }
            var items = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null).OrderBy(i => i.Id).ToList();
            db.SetItems(items);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"ItemDatabase: {items.Count} items");
        }

        // ================= data =================

        [MenuItem("Mayham/Shelter/1. Items, trader and placeholder art")]
        public static void SetupData()
        {
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory("Assets/Data/Traders");

            var coinSprite = MakeSprite("Assets/Sprites/Items/Coin.png", 16, 16, DrawCoin);
            MakeSprite(SpriteDir + "/Bed.png", 32, 48, DrawBed);
            MakeSprite(SpriteDir + "/ModuleSlot.png", 32, 32, DrawModuleSlot);
            MakeSprite(SpriteDir + "/ExitMark.png", 32, 16, DrawExitMark);

            // Prices of the existing items.
            var values = new Dictionary<string, int> { { "Mug", 6 }, { "Mop", 12 }, { "Books", 8 }, { "Folders", 4 }, { "Papers", 2 }, { "Notes", 3 } };
            foreach (var pair in values)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{pair.Key}.asset");
                if (item == null) continue;
                var so = new SerializedObject(item);
                so.FindProperty("value").intValue = pair.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var coin = EnsureCoin(coinSprite);
            RefreshItemDatabase();
            EnsureTrader(coin);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static ItemDefinition EnsureCoin(Sprite sprite)
        {
            const string itemPath = "Assets/Data/Items/Coin.asset";
            const string prefabPath = "Assets/Prefabs/Interactable/Coin.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                AssetDatabase.CopyAsset("Assets/Prefabs/Interactable/Mug.prefab", prefabPath);

            var coin = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (coin == null)
            {
                coin = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(coin, itemPath);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var so = new SerializedObject(coin);
            so.FindProperty("displayName").stringValue = "Монета";
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.FindProperty("maxStack").intValue = 50;
            so.FindProperty("gridSize").vector2IntValue = Vector2Int.one;
            so.FindProperty("weight").floatValue = 0.02f;
            so.FindProperty("value").intValue = 2;
            so.FindProperty("holdable").boolValue = false;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            contents.name = "Coin";
            contents.transform.localScale = Vector3.one * 0.5f;
            contents.GetComponent<SpriteRenderer>().sprite = sprite;
            var pickup = new SerializedObject(contents.GetComponent<ItemPickup>());
            pickup.FindProperty("item").objectReferenceValue = coin;
            pickup.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            EditorUtility.SetDirty(coin);
            return coin;
        }

        private static void EnsureTrader(ItemDefinition coin)
        {
            var trader = AssetDatabase.LoadAssetAtPath<TraderDefinition>(TraderPath);
            if (trader == null)
            {
                trader = ScriptableObject.CreateInstance<TraderDefinition>();
                AssetDatabase.CreateAsset(trader, TraderPath);
            }

            ItemDefinition Item(string name) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{name}.asset");

            var so = new SerializedObject(trader);
            so.FindProperty("displayName").stringValue = "Тайлер";
            so.FindProperty("portrait").objectReferenceValue = PlayerSprite();
            so.FindProperty("greeting").stringValue = "Здорово, [ИГРОК]. Присаживайся, смотри, что у меня есть. Что-то продашь — заплачу честно.";
            so.FindProperty("sellMarkup").floatValue = 1.3f;
            so.FindProperty("buyRate").floatValue = 0.5f;

            var stock = new (ItemDefinition item, int count)[]
            {
                (coin, 30), (Item("Mug"), 2), (Item("Papers"), 12), (Item("Folders"), 6), (Item("Books"), 3), (Item("Notes"), 4), (Item("Mop"), 1)
            };
            var stockProp = so.FindProperty("stock");
            stockProp.arraySize = stock.Length;
            for (int i = 0; i < stock.Length; i++)
            {
                var entry = stockProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = stock[i].item;
                entry.FindPropertyRelative("count").intValue = stock[i].count;
                entry.FindPropertyRelative("priceOverride").intValue = 0;
            }

            // Sample barter offers: the recipes themselves are added later in the asset.
            var offers = new (string title, (ItemDefinition, int)[] give, (ItemDefinition, int)[] get)[]
            {
                ("Кружка на папки", new[] { (Item("Mug"), 1) }, new[] { (Item("Folders"), 2) }),
                ("Швабра на книги", new[] { (Item("Mop"), 1) }, new[] { (Item("Books"), 2) }),
                ("Заметки на монеты", new[] { (Item("Notes"), 3) }, new[] { (coin, 10) })
            };
            var barter = so.FindProperty("barter");
            barter.arraySize = offers.Length;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = barter.GetArrayElementAtIndex(i);
                offer.FindPropertyRelative("title").stringValue = offers[i].title;
                FillAmounts(offer.FindPropertyRelative("playerGives"), offers[i].give);
                FillAmounts(offer.FindPropertyRelative("playerReceives"), offers[i].get);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(trader);
        }

        private static void FillAmounts(SerializedProperty list, (ItemDefinition item, int count)[] amounts)
        {
            list.arraySize = amounts.Length;
            for (int i = 0; i < amounts.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = amounts[i].item;
                element.FindPropertyRelative("count").intValue = amounts[i].count;
            }
        }

        private static Sprite PlayerSprite()
        {
            // The character art the player has in the scenes (the prefab still carries an older placeholder).
            return AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/PlayerHero.png").OfType<Sprite>().FirstOrDefault();
        }

        // ================= placeholder art =================

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

        private static void Fill(Color[] p, int w, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++) p[y * w + x] = c;
            }
        }

        private static void DrawCoin(Color[] p, int w, int h)
        {
            var edge = new Color(0.62f, 0.45f, 0.08f);
            var gold = new Color(0.97f, 0.79f, 0.2f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(w / 2f, h / 2f));
                    p[y * w + x] = d <= 5.5f ? gold : d <= 6.7f ? edge : Color.clear;
                }
            }
            Fill(p, w, 6, 6, 9, 9, new Color(0.8f, 0.6f, 0.12f));
            Fill(p, w, 5, 9, 6, 10, new Color(1f, 0.95f, 0.6f));
        }

        private static void DrawBed(Color[] p, int w, int h)
        {
            var frame = new Color(0.36f, 0.24f, 0.14f);
            var mattress = new Color(0.82f, 0.84f, 0.88f);
            var blanket = new Color(0.25f, 0.45f, 0.55f);
            var fold = new Color(0.17f, 0.33f, 0.42f);
            Fill(p, w, 0, 0, w - 1, h - 1, frame);
            Fill(p, w, 2, 2, w - 3, h - 3, mattress);
            Fill(p, w, 4, 36, w - 5, h - 6, Color.white);              // pillow
            Fill(p, w, 5, 37, w - 6, h - 7, new Color(0.93f, 0.94f, 0.97f));
            Fill(p, w, 2, 2, w - 3, 30, blanket);
            Fill(p, w, 2, 28, w - 3, 30, fold);
            Fill(p, w, 2, 12, w - 3, 12, fold);
        }

        private static void DrawModuleSlot(Color[] p, int w, int h)
        {
            var c = new Color(0.8f, 0.85f, 0.95f, 0.75f);
            for (int i = 1; i < w - 1; i++)
            {
                if ((i / 3) % 2 != 0) continue;
                p[1 * w + i] = c; p[(h - 2) * w + i] = c; p[i * w + 1] = c; p[i * w + (w - 2)] = c;
            }
            Fill(p, w, w / 2 - 4, h / 2 - 1, w / 2 + 3, h / 2, c);   // plus sign
            Fill(p, w, w / 2 - 1, h / 2 - 4, w / 2, h / 2 + 3, c);
        }

        private static void DrawExitMark(Color[] p, int w, int h)
        {
            var yellow = new Color(0.95f, 0.8f, 0.15f);
            var black = new Color(0.1f, 0.1f, 0.1f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    p[y * w + x] = border ? black : ((x + y) / 4) % 2 == 0 ? yellow : black;
                }
            }
        }

        // ================= Shelter scene =================

        [MenuItem("Mayham/Shelter/2. Build Shelter scene")]
        public static void BuildShelterScene()
        {
            var main = SceneManager.GetSceneByPath(MainScenePath);
            if (main.IsValid() && main.isDirty) EditorSceneManager.SaveScene(main);

            AssetDatabase.DeleteAsset(ShelterScenePath);
            AssetDatabase.CopyAsset(MainScenePath, ShelterScenePath);
            var scene = EditorSceneManager.OpenScene(ShelterScenePath, OpenSceneMode.Single);

            Strip(scene);
            var floor = FindRoomMap("Floor");
            var walls = FindRoomMap("Walls");
            PaintRoom(floor, walls);
            Furnish();
            SetUpPlayerAndCamera();

            var hud = GameObject.Find("HUD");
            if (hud.GetComponent<MoneyHUD>() == null) hud.AddComponent<MoneyHUD>();

            AddToBuildSettings(ShelterScenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Shelter scene built.");
        }

        private static void Strip(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Legacy (old office)" || root.name == "Enemies" || root.name == "RaidController") Object.DestroyImmediate(root);
            }

            var maze = GameObject.Find("Maze");
            // Keep only the two tilemaps: everything else (room slots, exit, spawn) belongs to the maze.
            for (int i = maze.transform.childCount - 1; i >= 0; i--)
            {
                var child = maze.transform.GetChild(i);
                if (child.name != "Floor" && child.name != "Walls") Object.DestroyImmediate(child.gameObject);
            }
            var generator = maze.GetComponent<RaidGenerator>();
            if (generator != null) Object.DestroyImmediate(generator);
            var wrap = maze.GetComponent<ProjectMayham.World.WrapWorld>();
            if (wrap != null) Object.DestroyImmediate(wrap);
            maze.name = "Room";
        }

        private static Tilemap FindRoomMap(string name) => GameObject.Find("Room").transform.Find(name).GetComponent<Tilemap>();

        private static T Rule<T>(string path) where T : TileBase => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void PaintRoom(Tilemap floor, Tilemap walls)
        {
            floor.ClearAllTiles();
            walls.ClearAllTiles();
            var wood = Rule<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Wood_01.asset");
            var stone = Rule<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_01.asset");
            var wall = Rule<TileBase>("Assets/TileSet/Rule Tiles/Walls/Wall-Concrete_01.asset");

            // Interior x -10..9, y -6..5; a 2-wide doorway in the bottom wall leads to the exit alcove (y -9..-8).
            for (int x = -10; x <= 9; x++)
            {
                for (int y = -6; y <= 5; y++) floor.SetTile(new Vector3Int(x, y, 0), wood);
            }
            for (int x = -11; x <= 10; x++)
            {
                walls.SetTile(new Vector3Int(x, 6, 0), wall);
                if (x != -1 && x != 0) walls.SetTile(new Vector3Int(x, -7, 0), wall);
            }
            for (int y = -6; y <= 5; y++)
            {
                walls.SetTile(new Vector3Int(-11, y, 0), wall);
                walls.SetTile(new Vector3Int(10, y, 0), wall);
            }
            // Doorway and the alcove behind it.
            for (int y = -9; y <= -7; y++)
            {
                floor.SetTile(new Vector3Int(-1, y, 0), stone);
                floor.SetTile(new Vector3Int(0, y, 0), stone);
                walls.SetTile(new Vector3Int(-2, y, 0), wall);
                walls.SetTile(new Vector3Int(1, y, 0), wall);
            }
            for (int x = -2; x <= 1; x++) walls.SetTile(new Vector3Int(x, -10, 0), wall);

            floor.CompressBounds();
            walls.CompressBounds();
            RebuildWallCollider(walls.gameObject);
        }

        // The copied composite collider keeps the maze's cached outlines; fresh components are generated from the new tiles.
        private static void RebuildWallCollider(GameObject walls)
        {
            Object.DestroyImmediate(walls.GetComponent<TilemapCollider2D>());
            Object.DestroyImmediate(walls.GetComponent<CompositeCollider2D>());
            Object.DestroyImmediate(walls.GetComponent<Rigidbody2D>());

            var body = walls.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = walls.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = walls.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = walls.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.GenerateGeometry();
        }

        private static Transform furnitureRoot;

        private static void Furnish()
        {
            var root = new GameObject("Shelter").transform;
            furnitureRoot = root;

            // --- furniture and decor ---
            Prop("Bed", SpriteDir + "/Bed.png", new Vector2(-8f, 4.5f), new Vector2(1.9f, 2.9f));
            Prop("Stash Cabinet", FurnitureDir + "Big-Filing-Cabinet.png", new Vector2(-0.5f, 5f), new Vector2(0.9f, 1.4f), new Vector2(0f, -0.2f));
            Prop("Stash Cabinet", FurnitureDir + "Big-Filing-Cabinet.png", new Vector2(1.5f, 5f), new Vector2(0.9f, 1.4f), new Vector2(0f, -0.2f));
            Prop("Bookshelf", FurnitureDir + "Tall-Bookshelf.png", new Vector2(-5f, 5f), new Vector2(0.9f, 1.6f));
            Prop("Bookshelf", FurnitureDir + "Tall-Bookshelf.png", new Vector2(-3f, 5f), new Vector2(0.9f, 1.6f));
            Prop("Desk", FurnitureDir + "Desk-2.png", new Vector2(7f, 4f), new Vector2(1.8f, 1.5f));
            Prop("Chair", FurnitureDir + "Chair.png", new Vector2(5.5f, 4.5f), Vector2.zero);
            Prop("Table", FurnitureDir + "Small-Table.png", new Vector2(9.5f, 4.5f), new Vector2(0.8f, 0.7f));
            Prop("Coffee", FurnitureDir + "Coffee-Machine.png", new Vector2(9.5f, 4.5f), Vector2.zero, order: 6);
            Prop("Crate", "Assets/Sprites/Level/Crate.png", new Vector2(3.5f, 5.5f), new Vector2(0.9f, 0.9f));
            Prop("Crate", "Assets/Sprites/Level/Box.png", new Vector2(4.5f, 5.5f), new Vector2(0.9f, 0.9f));
            Prop("Sofa", FurnitureDir + "Big-Sofa.png", new Vector2(-8f, -0.5f), new Vector2(1.5f, 1.3f));
            Prop("Table", FurnitureDir + "Small-Table.png", new Vector2(-6f, -0.5f), new Vector2(0.8f, 0.7f));
            Prop("Plant", FurnitureDir + "Big-Plant.png", new Vector2(-9f, -2.5f), new Vector2(1f, 1.4f), new Vector2(0f, -0.2f));
            Prop("Plant", FurnitureDir + "Big-Plant.png", new Vector2(9f, -2.5f), new Vector2(1f, 1.4f), new Vector2(0f, -0.2f));
            Prop("Water", FurnitureDir + "Water-Dispenser.png", new Vector2(9f, 1f), new Vector2(0.7f, 1.6f));
            Prop("Clock", FurnitureDir + "Wall-Clock.png", new Vector2(-2f, 5.5f), Vector2.zero, order: 2);

            // --- exit alcove ---
            var mark = Prop("Exit Mark", SpriteDir + "/ExitMark.png", new Vector2(0f, -8.5f), Vector2.zero, order: 2);
            var labelObject = new GameObject("Exit Label", typeof(RectTransform));
            labelObject.transform.SetParent(root, false);
            labelObject.transform.position = new Vector3(0f, -7.6f, 0f);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = "ВЫХОД В РЕЙД";
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.95f, 0.8f, 0.15f);
            label.rectTransform.sizeDelta = new Vector2(6f, 1f);
            label.GetComponent<MeshRenderer>().sortingOrder = 8;

            var exit = new GameObject("Exit Zone", typeof(BoxCollider2D), typeof(ShelterExit));
            exit.transform.SetParent(root, false);
            exit.transform.position = mark.transform.position;
            var zone = exit.GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
            zone.size = new Vector2(2f, 1f);

            // --- interactables ---
            var bed = Point("Bed Interaction", new Vector2(-6.6f, 3.6f));
            bed.AddComponent<Bed>();

            var stash = Point("Stash", new Vector2(0.5f, 3.9f)).AddComponent<StashChest>();
            var inventory = stash.GetComponent<GridInventory>();
            var inv = new SerializedObject(inventory);
            inv.FindProperty("gridSize").vector2IntValue = new Vector2Int(10, 10);
            inv.FindProperty("slotCount").intValue = 1;
            inv.FindProperty("comfortableWeight").floatValue = 100000f;
            inv.FindProperty("maxWeight").floatValue = 100000f;
            inv.ApplyModifiedPropertiesWithoutUndo();

            var npc = new GameObject("Tyler", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(NpcTrader));
            npc.transform.SetParent(root, false);
            npc.transform.position = new Vector3(7.5f, 2.4f, 0f);
            npc.layer = LayerMask.NameToLayer("Furniture");
            var body = npc.GetComponent<SpriteRenderer>();
            body.sprite = PlayerSprite();
            body.color = new Color(0.9f, 0.75f, 0.55f);
            body.sortingOrder = 10;
            npc.GetComponent<CircleCollider2D>().radius = 0.35f;
            var npcData = new SerializedObject(npc.GetComponent<NpcTrader>());
            npcData.FindProperty("trader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TraderDefinition>(TraderPath);
            npcData.ApplyModifiedPropertiesWithoutUndo();

            // Four places for future modules.
            var slots = new[] { new Vector2(-8f, -5f), new Vector2(-5f, -5f), new Vector2(5f, -5f), new Vector2(8f, -5f) };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = Prop($"Module Slot {i + 1}", SpriteDir + "/ModuleSlot.png", slots[i], Vector2.zero, order: 2);
                var module = slot.AddComponent<ShelterModuleSlot>();
                var data = new SerializedObject(module);
                data.FindProperty("slotId").stringValue = $"Slot{i + 1}";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject Point(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(furnitureRoot, false);
            go.transform.position = position;
            return go;
        }

        /// <summary>A sprite prop. With a collider size it blocks movement (furniture layer).</summary>
        private static GameObject Prop(string name, string spritePath, Vector2 position, Vector2 colliderSize,
            Vector2 colliderOffset = default, int order = 5)
        {
            var go = new GameObject(name);
            go.transform.SetParent(furnitureRoot, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            renderer.sortingOrder = order;
            if (colliderSize != Vector2.zero)
            {
                go.layer = LayerMask.NameToLayer("Furniture");
                var box = go.AddComponent<BoxCollider2D>();
                box.size = colliderSize;
                box.offset = colliderOffset;
            }
            return go;
        }

        private static void SetUpPlayerAndCamera()
        {
            var player = GameObject.Find("Player");
            player.transform.position = new Vector3(0f, -3.5f, 0f);
            if (player.GetComponent<PlayerPersistence>() == null) player.AddComponent<PlayerPersistence>();
            player.GetComponent<VisionCone>().enabled = false; // a safe room: no fog of war

            var camera = Camera.main;
            camera.transform.position = new Vector3(0f, -3.5f, camera.transform.position.z);
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<VisionRenderer>().enabled = false;
        }

        // ================= raid scene =================

        [MenuItem("Mayham/Shelter/3. Patch MainScene (raid)")]
        public static void PatchRaidScene()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            var persistence = player.GetComponent<PlayerPersistence>();
            if (persistence == null) persistence = player.AddComponent<PlayerPersistence>();

            var hud = GameObject.Find("HUD");
            if (hud.GetComponent<MoneyHUD>() == null) hud.AddComponent<MoneyHUD>();

            var controllerObject = GameObject.Find("RaidController");
            if (controllerObject == null) controllerObject = new GameObject("RaidController");
            var controller = controllerObject.GetComponent<RaidController>();
            if (controller == null) controller = controllerObject.AddComponent<RaidController>();
            var data = new SerializedObject(controller);
            data.FindProperty("stats").objectReferenceValue = player.GetComponent<PlayerStats>();
            data.FindProperty("persistence").objectReferenceValue = persistence;
            data.FindProperty("inventoryWindow").objectReferenceValue = hud.GetComponentInChildren<InventoryWindow>(true);
            data.ApplyModifiedPropertiesWithoutUndo();

            AddToBuildSettings(ShelterScenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MainScene patched.");
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;

            // The shelter comes right after the main menu.
            int menu = scenes.FindIndex(s => s.path.EndsWith("MainMenu.unity"));
            scenes.Insert(menu + 1, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
