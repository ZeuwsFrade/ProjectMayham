using System.IO;
using System.Linq;
using ProjectMayham.AI;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Tutorial;
using ProjectMayham.UI;
using ProjectMayham.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Tutorial.unity, the level a new game starts with, from a copy of MainScene (player,
    /// camera, HUD, vision, navigation) with the maze taken out and three rooms painted in its place:
    /// 1) a room to search for food and bandages, 2) two Dokhodyagi to sneak past and lure away with a mug,
    /// 3) a cooler, a toilet, a hole, a barricade and the locked exit with its key in a crate. Also makes the key
    /// item and the placeholder door sprite, and puts the scene into the build. Run it again to rebuild from scratch.
    /// Mayham > Tutorial > Build the tutorial scene.
    /// </summary>
    public static class TutorialSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tutorial.unity";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string KeyPath = "Assets/Data/Items/TutorialKey.asset";
        private const string KeyIconPath = "Assets/Sprites/Items/TutorialKey.png";
        private const string KeyPickupPath = "Assets/Prefabs/Interactable/TutorialKey.prefab";
        private const string DoorSpritePath = "Assets/Sprites/Level/Door.png";
        private const string PickupTemplate = "Assets/Prefabs/Interactable/Mug.prefab";
        private const string Furniture = "Assets/Prefabs/Furniture/";
        private const string Interactable = "Assets/Prefabs/Interactable/";
        private const string Consumables = "Assets/Data/Items/Consumables/";
        private const int PixelsPerUnit = 16;

        private static Transform root;
        private static Tilemap floor;
        private static Tilemap walls;
        private static TileBase wall;

        [MenuItem("Mayham/Tutorial/Build the tutorial scene")]
        public static void Build()
        {
            EnsureKey();
            EnsureSprite(DoorSpritePath, DrawDoor);

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(MainScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Loaded again: opening the scene unloads assets that nothing referenced, and new ones with them.
            var key = Load<ItemDefinition>(KeyPath);
            var doorSprite = Load<Sprite>(DoorSpritePath);

            Strip();
            var level = GameObject.Find("Level").transform;
            floor = level.Find("Floor").GetComponent<Tilemap>();
            walls = level.Find("Walls").GetComponent<Tilemap>();
            wall = Load<TileBase>("Assets/TileSet/Rule Tiles/Walls/Wall-Concrete_01.asset");
            floor.ClearAllTiles();
            walls.ClearAllTiles();

            root = new GameObject("Tutorial").transform;
            var director = new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
            director.transform.SetParent(root, false);

            var starts = new Transform[3];
            var firstGate = StageOne(doorSprite, starts);
            var guards = StageTwo(starts);
            var exitDoor = StageThree(doorSprite, key, starts);

            walls.CompressBounds();
            floor.CompressBounds();
            RebuildWallCollider(walls.gameObject);

            var player = GameObject.Find("Player");
            player.transform.position = starts[0].position;
            var camera = Camera.main;
            camera.transform.position = new Vector3(starts[0].position.x, starts[0].position.y, camera.transform.position.z);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<Player.PlayerStats>();
            so.FindProperty("inventoryWindow").objectReferenceValue = Object.FindFirstObjectByType<InventoryWindow>(FindObjectsInactive.Include);
            var startList = so.FindProperty("stageStarts");
            startList.arraySize = starts.Length;
            for (int i = 0; i < starts.Length; i++) startList.GetArrayElementAtIndex(i).objectReferenceValue = starts[i];
            so.FindProperty("firstGate").objectReferenceValue = firstGate;
            so.FindProperty("exitDoor").objectReferenceValue = exitDoor;
            so.FindProperty("bandage").objectReferenceValue = Load<ItemDefinition>(Consumables + "Bandage.asset");
            so.FindProperty("mug").objectReferenceValue = Load<ItemDefinition>("Assets/Data/Items/Mug.asset");
            so.FindProperty("key").objectReferenceValue = key;
            var guardList = so.FindProperty("guards");
            guardList.arraySize = guards.Length;
            for (int i = 0; i < guards.Length; i++)
            {
                var element = guardList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("enemy").objectReferenceValue = guards[i].enemy;
                element.FindPropertyRelative("facing").vector2Value = guards[i].facing;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuild();
            Debug.Log("TutorialSceneBuilder: " + ScenePath);
        }

        // MainScene's raid goes; the player, camera, HUD, light and navigation stay.
        private static void Strip()
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                // The tutorial has its own rules of death and leaving: no RaidController.
                if (go.name == "Enemies" || go.name == "Raid Enemies" || go.name == "EnemySpawner" || go.name == "RaidController")
                    Object.DestroyImmediate(go);
            }

            var maze = GameObject.Find("Maze");
            for (int i = maze.transform.childCount - 1; i >= 0; i--)
            {
                var child = maze.transform.GetChild(i);
                if (child.name != "Floor" && child.name != "Walls") Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(maze.GetComponent<RaidGenerator>());
            Object.DestroyImmediate(maze.GetComponent<WrapWorld>());
            maze.name = "Level";
        }

        // ---- stage 1: search the room ----

        private static Door StageOne(Sprite doorSprite, Transform[] starts)
        {
            var stage = Group("Stage 1");
            var wood = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Wood_01.asset");
            Room(0, 0, 11, 9, wood);
            starts[0] = Point(stage, "Start", 2.5f, 2.5f);

            Sign(stage, 1.5f, 1.2f, "Записка",
                "Если ты это читаешь — ты очнулся. Здание кишит Доходягами, но здесь пока тихо.\n\n" +
                "• WASD — идти, Shift — бежать (тратит выносливость).\n" +
                "• E — обыскать мебель или подобрать вещь. I — инвентарь.\n" +
                "• В инвентаре наведи курсор на еду и нажми F — съешь. ПКМ — взять вещь в руки, тогда зажми ЛКМ.\n\n" +
                "Ты ранен, а голод съедает выносливость. Найди еду и бинты.");

            Container(stage, Interactable + "Filing-Cabinet-Tall.prefab", 1.5f, 8.6f, ("Papers", 3));
            Container(stage, Interactable + "Wide-Filing-Cabinet.prefab", 6f, 8.8f, ("Consumables/Bandage", 2));
            Container(stage, Interactable + "Bin.prefab", 10.4f, 8.5f, ("Notes", 1));
            Container(stage, Furniture + "Crate.prefab", 10.5f, 1.5f, ("Consumables/Sandwich", 1), ("Consumables/Bar", 1));

            Place(stage, Furniture + "Big-Sofa.prefab", 4.5f, 5f);
            Place(stage, Furniture + "Small-Table.prefab", 6.5f, 5f);
            Place(stage, Furniture + "Big-Plant.prefab", 0.6f, 5.5f);

            // The door to the second room, shut until the goals are done.
            return Doorway(stage, doorSprite, 12, 4, vertical: true, "Дверь", "Сначала поешьте и перевяжитесь", null);
        }

        // ---- stage 2: sneak and lure ----

        private static (EnemyAI enemy, Vector2 facing)[] StageTwo(Transform[] starts)
        {
            var stage = Group("Stage 2");
            var stone = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_01.asset");
            var tiles = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_02.asset");
            Room(13, 0, 24, 9, stone);
            Room(26, 0, 37, 9, tiles);
            Opening(25, 3, 25, 4, stone);
            starts[1] = Point(stage, "Start", 14.5f, 4.5f);
            Zone(stage, TutorialDirector.StageTwoZone, 14f, 5f, 2f, 4f);

            Sign(stage, 13.6f, 7f, "Предупреждение",
                "Доходяги слабы поодиночке, но зовут своих.\n\n" +
                "• Зажми Ctrl — присядешь: шаги не слышны, и заметить тебя могут только вблизи.\n" +
                "• Обычные шаги слышно рядом, бег — издалека.\n" +
                "• Не попадайся им на глаза: смотрят они вперёд, а не за спину.\n" +
                "• Подобранная вещь выбрана в инвентаре. Зажми G, наведи курсор и отпусти — она упадёт с шумом, и враги пойдут проверить.");

            // The first room: clutter along the bottom leaves a lane that passes right behind the first guard.
            Place(stage, Furniture + "Desk-2.prefab", 16f, 1.1f);
            Place(stage, Furniture + "Desk-2.prefab", 20f, 1.1f);
            Place(stage, Furniture + "Box.prefab", 22.5f, 0.6f);
            Place(stage, Furniture + "Bookshelf.prefab", 15f, 9.3f);
            var first = Guard(stage, 19.5f, 7.5f, Vector2.up);
            Zone(stage, TutorialDirector.PastGuardZone, 23.5f, 5f, 3f, 10f);
            Place(stage, Interactable + "Mug.prefab", 23.4f, 2.4f);

            // The second room: the guard stands right in front of the way out. A mug thrown into the upper corner
            // near him (about (33, 8.5)) draws him away from the lower way to the door.
            Zone(stage, TutorialDirector.GuardRoomZone, 27f, 5f, 2f, 10f);
            Place(stage, Furniture + "Tall-Bookshelf.prefab", 30.5f, 7.5f);
            Place(stage, Furniture + "Desk.prefab", 33.5f, 1.2f);
            Place(stage, Furniture + "Small-Plant.prefab", 37.4f, 9.4f);
            var second = Guard(stage, 36.5f, 6.5f, Vector2.left);

            // Out of the second room into a short corridor: reaching it ends the stage.
            Opening(38, 6, 38, 7, tiles);
            Room(39, 6, 40, 7, tiles);
            Zone(stage, TutorialDirector.StageThreeZone, 40f, 7f, 2f, 2f);

            return new[] { (first, Vector2.up), (second, Vector2.left) };
        }

        // ---- stage 3: the world ----

        private static Door StageThree(Sprite doorSprite, ItemDefinition key, Transform[] starts)
        {
            var stage = Group("Stage 3");
            var metal = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Metal_01.asset");
            var wood = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Wood_02.asset");
            var stone = Load<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_03.asset");
            starts[2] = Point(stage, "Start", 40f, 7f);

            // The washroom: a cooler and a toilet.
            Room(42, 0, 51, 9, metal);
            Opening(41, 6, 41, 7, metal);
            Sign(stage, 42.6f, 4.5f, "Памятка",
                "• Жажда замедляет лечение, голод забирает выносливость.\n" +
                "• Кулер: подойди и удерживай E — утолишь жажду. Вода быстро просится наружу.\n" +
                "• Когда сильно приспичит, ты замедлишься, а враги учуют запах. Туалет: удерживай E.\n" +
                "• Дальше дороги нет — но в стене справа дыра. Удерживай E рядом с ней, чтобы пролезть.");
            Place(stage, Interactable + "Water-Dispenser.prefab", 44f, 9.2f);
            Place(stage, Furniture + "Toilet-Closed.prefab", 50.5f, 9.2f);
            Place(stage, Furniture + "WC-Sink.prefab", 48.5f, 9.3f);

            // A hole through the wall into the next room, the only way on.
            Room(53, 0, 62, 9, wood);
            Hole(stage, 52, 2, metal);
            Zone(stage, TutorialDirector.PastHoleZone, 58f, 5f, 10f, 10f);
            Place(stage, Furniture + "Desk-2.prefab", 57f, 7.5f);
            Place(stage, Furniture + "Chair.prefab", 57f, 6.4f);

            // A barricade in the doorway: only climbing gets the player over it.
            Room(64, 0, 71, 9, stone);
            Opening(63, 4, 63, 5, stone);
            Barricade(stage, 63.5f, 5f);
            Zone(stage, TutorialDirector.PastBarricadeZone, 68f, 5f, 8f, 10f);

            // The crates: the key is in the last one.
            Container(stage, Furniture + "Box.prefab", 66.5f, 8.5f, ("Papers", 2));
            Container(stage, Furniture + "Crate.prefab", 69.5f, 8.5f, ("Coin", 4));
            Container(stage, Furniture + "Crate.prefab", 70.5f, 1.5f, (key, 1));

            // The way out.
            var exit = Doorway(stage, doorSprite, 72, 4, vertical: true, "Дверь наружу", "Заперто", key);
            Room(73, 3, 75, 6, stone);
            var sign = new GameObject("ExitSign");
            sign.transform.SetParent(stage, false);
            sign.transform.position = new Vector3(74.5f, 5f, 0f);
            var renderer = sign.AddComponent<SpriteRenderer>();
            renderer.sprite = Load<Sprite>("Assets/Sprites/Level/Exit.png");
            renderer.sortingOrder = 2;
            Zone(stage, TutorialDirector.FinishZone, 74.5f, 5f, 3f, 4f);
            return exit;
        }

        // ---- painting ----

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        /// <summary>Floor over x0..x1, y0..y1 (cells, inclusive) and a ring of wall round it; walls already there stay.</summary>
        private static void Room(int x0, int y0, int x1, int y1, TileBase tile)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    floor.SetTile(new Vector3Int(x, y, 0), tile);
                    walls.SetTile(new Vector3Int(x, y, 0), null);
                }
            for (int x = x0 - 1; x <= x1 + 1; x++)
            {
                WallAt(x, y0 - 1);
                WallAt(x, y1 + 1);
            }
            for (int y = y0; y <= y1; y++)
            {
                WallAt(x0 - 1, y);
                WallAt(x1 + 1, y);
            }
        }

        private static void WallAt(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            if (!floor.HasTile(cell)) walls.SetTile(cell, wall);
        }

        /// <summary>Takes the wall out of x0..x1, y0..y1 and puts floor there: a doorway.</summary>
        private static void Opening(int x0, int y0, int x1, int y1, TileBase tile)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    walls.SetTile(new Vector3Int(x, y, 0), null);
                    floor.SetTile(new Vector3Int(x, y, 0), tile);
                }
        }

        // The copied composite collider keeps the old outlines; fresh components are made from the new tiles.
        private static void RebuildWallCollider(GameObject wallsObject)
        {
            Object.DestroyImmediate(wallsObject.GetComponent<TilemapCollider2D>());
            Object.DestroyImmediate(wallsObject.GetComponent<CompositeCollider2D>());
            Object.DestroyImmediate(wallsObject.GetComponent<Rigidbody2D>());

            var body = wallsObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = wallsObject.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = wallsObject.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = wallsObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.GenerateGeometry();
        }

        // ---- placing things ----

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        private static Transform Point(Transform parent, string name, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            return go.transform;
        }

        private static GameObject Place(Transform parent, string prefabPath, float x, float y, float angle = 0f)
        {
            var prefab = Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"TutorialSceneBuilder: no prefab at {prefabPath}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return go;
        }

        private static void Container(Transform parent, string prefabPath, float x, float y, params (string item, int count)[] contents) =>
            Container(parent, prefabPath, x, y, contents.Select(c => (Load<ItemDefinition>($"Assets/Data/Items/{c.item}.asset"), c.count)).ToArray());

        private static void Container(Transform parent, string prefabPath, float x, float y, params (ItemDefinition item, int count)[] contents)
        {
            var go = Place(parent, prefabPath, x, y);
            if (go == null) return;
            if (go.GetComponent<LootContainer>() == null)
            {
                Debug.LogError($"TutorialSceneBuilder: {prefabPath} is not a loot container");
                return;
            }

            var preset = go.AddComponent<PresetLoot>();
            var so = new SerializedObject(preset);
            var list = so.FindProperty("contents");
            list.arraySize = contents.Length;
            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i].item == null) Debug.LogError($"TutorialSceneBuilder: a missing item in {go.name}");
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = contents[i].item;
                element.FindPropertyRelative("count").intValue = contents[i].count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Sign(Transform parent, float x, float y, string title, string text)
        {
            var go = new GameObject("Sign: " + title);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Load<Sprite>("Assets/Sprites/Office Furniture/Wall-Note.png");
            renderer.sortingOrder = 4;
            go.transform.localScale = Vector3.one * 0.6f;
            var sign = go.AddComponent<TutorialSign>();
            var so = new SerializedObject(sign);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("text").stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Zone(Transform parent, string id, float x, float y, float width, float height)
        {
            var go = new GameObject("Zone: " + id) { layer = LayerMask.NameToLayer("Ignore Raycast") };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(width, height);
            var zone = go.AddComponent<TutorialZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemyAI Guard(Transform parent, float x, float y, Vector2 facing)
        {
            var go = Place(parent, "Assets/Prefabs/Enemies/Dokhodyaga.prefab", x, y);
            go.transform.up = facing;
            return go.GetComponent<EnemyAI>();
        }

        /// <summary>A two cells wide doorway at (x, y) and the cell above it (or to the right), closed by a door.</summary>
        private static Door Doorway(Transform parent, Sprite sprite, int x, int y, bool vertical, string title, string lockedHint, ItemDefinition key)
        {
            var under = floor.GetTile(new Vector3Int(x - 1, y, 0));
            if (vertical) Opening(x, y, x, y + 1, under);
            else Opening(x, y, x + 1, y, under);

            var go = new GameObject(title) { layer = LayerMask.NameToLayer("Obstacle") };
            go.transform.SetParent(parent, false);
            go.transform.position = vertical ? new Vector3(x + 0.5f, y + 1f, 0f) : new Vector3(x + 1f, y + 0.5f, 0f);
            if (!vertical) go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 6;
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 2f);

            var door = go.AddComponent<Door>();
            var so = new SerializedObject(door);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("lockedHint").stringValue = lockedHint;
            so.FindProperty("key").objectReferenceValue = key;
            so.FindProperty("locked").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        /// <summary>A hole through the wall cell (x, y) of a wall that runs up and down.</summary>
        private static void Hole(Transform parent, int x, int y, TileBase floorTile)
        {
            walls.SetTile(new Vector3Int(x, y, 0), null);
            floor.SetTile(new Vector3Int(x, y, 0), floorTile);
            // The prefab lies along X like a wall between a cell and the one above it; this wall needs a quarter turn.
            Place(parent, "Assets/Prefabs/Holes/Wall-Hole.prefab", x + 0.5f, y + 0.5f, 90f);
        }

        private static void Barricade(Transform parent, float x, float y)
        {
            var go = Place(parent, "Assets/Prefabs/Barricades/Barricade-Crates.prefab", x, y, 90f);
            var so = new SerializedObject(go.GetComponent<Barricade>());
            so.FindProperty("canPush").boolValue = false;
            so.FindProperty("canDismantle").boolValue = false;
            so.FindProperty("canClimb").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            // Right after the menu, before the shelter: the order a new game goes through.
            int menu = scenes.FindIndex(s => s.path == "Assets/Scenes/MainMenu.unity");
            scenes.Insert(menu + 1, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- the key and the door picture ----

        private static ItemDefinition EnsureKey()
        {
            var existing = Load<ItemDefinition>(KeyPath);
            if (existing != null) return existing;

            var icon = EnsureSprite(KeyIconPath, DrawKey);
            if (Load<GameObject>(KeyPickupPath) == null) AssetDatabase.CopyAsset(PickupTemplate, KeyPickupPath);
            var prefab = Load<GameObject>(KeyPickupPath);

            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = "Ключ от выхода";
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("maxStack").intValue = 1;
            so.FindProperty("gridSize").vector2IntValue = Vector2Int.one;
            so.FindProperty("weight").floatValue = 0.05f;
            so.FindProperty("value").intValue = 0;
            so.FindProperty("holdable").boolValue = false;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(item, KeyPath);

            // The pickup is a copy of the mug's: it shows the icon and hands out the key.
            var pickupRoot = PrefabUtility.LoadPrefabContents(KeyPickupPath);
            try
            {
                pickupRoot.name = "TutorialKey";
                var renderer = pickupRoot.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null) renderer.sprite = icon;
                var pso = new SerializedObject(pickupRoot.GetComponent<ItemPickup>());
                pso.FindProperty("item").objectReferenceValue = item;
                pso.FindProperty("count").intValue = 1;
                pso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(pickupRoot, KeyPickupPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(pickupRoot);
            }

            ShelterSetup.RefreshItemDatabase();
            return item;
        }

        private static Sprite EnsureSprite(string path, System.Func<Texture2D> draw)
        {
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, draw().EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return Load<Sprite>(path);
        }

        // A placeholder door seen from above: a plank 16 x 32 pixels (one cell by two) with a frame and a handle.
        private static Texture2D DrawDoor()
        {
            var texture = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            var frame = new Color(0.22f, 0.14f, 0.08f);
            var wood = new Color(0.52f, 0.34f, 0.18f);
            var grain = new Color(0.45f, 0.29f, 0.15f);
            var handle = new Color(0.85f, 0.75f, 0.35f);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 32; y++)
                {
                    bool edge = x < 2 || x > 13 || y < 1 || y > 30;
                    texture.SetPixel(x, y, edge ? frame : (x % 4 == 0 ? grain : wood));
                }
            for (int x = 6; x <= 9; x++)
                for (int y = 14; y <= 17; y++) texture.SetPixel(x, y, handle);
            texture.Apply();
            return texture;
        }

        // A placeholder key icon, 16 x 16: a ring and a toothed shaft.
        private static Texture2D DrawKey()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var gold = new Color(0.95f, 0.78f, 0.3f);
            var dark = new Color(0.55f, 0.4f, 0.12f);
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++) texture.SetPixel(x, y, clear);
            for (int x = 1; x <= 6; x++)
                for (int y = 5; y <= 10; y++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 7.5f));
                    if (d <= 3f) texture.SetPixel(x, y, d <= 1.2f ? clear : (d > 2.4f ? dark : gold));
                }
            for (int x = 6; x <= 14; x++)
            {
                texture.SetPixel(x, 7, gold);
                texture.SetPixel(x, 8, gold);
                texture.SetPixel(x, 6, dark);
            }
            for (int y = 3; y <= 5; y++)
            {
                texture.SetPixel(12, y, gold);
                texture.SetPixel(14, y, gold);
            }
            texture.Apply();
            return texture;
        }
    }
}
