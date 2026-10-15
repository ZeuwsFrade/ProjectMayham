using ProjectMayham.AI;
using ProjectMayham.Combat;
using ProjectMayham.Level;
using ProjectMayham.Vision;
using ProjectMayham.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Tests/CombatTest.unity: a small arena for trying the fighting out, made from a copy of
    /// MainScene (player, camera, HUD, vision) with the maze taken out. Run it again to rebuild from scratch.
    /// </summary>
    public static class CombatTestScene
    {
        public const string ScenePath = "Assets/Scenes/Tests/CombatTest.unity";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string FurnitureDir = "Assets/Sprites/Office Furniture/";

        private static Transform props;

        [MenuItem("Mayham/Combat/3. Build the combat test scene")]
        public static void Build()
        {
            if (!System.IO.File.Exists("Assets/Prefabs/Enemies/Dokhodyaga.prefab"))
            {
                Debug.LogError("CombatTestScene: run Mayham/Combat/1 first (weapons and enemy).");
                return;
            }

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(MainScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Strip();
            var floor = GameObject.Find("Arena").transform.Find("Floor").GetComponent<Tilemap>();
            var walls = GameObject.Find("Arena").transform.Find("Walls").GetComponent<Tilemap>();
            Paint(floor, walls);

            props = new GameObject("Props").transform;
            Furnish();
            Dummies();
            Pickups();
            Tools();

            var player = GameObject.Find("Player");
            player.transform.position = new Vector3(0f, -8f, 0f);
            var camera = Camera.main;
            camera.transform.position = new Vector3(0f, -8f, camera.transform.position.z);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("CombatTestScene: " + ScenePath);
        }

        // MainScene's raid scenery goes; the player, camera, HUD, light, navigation and spawner stay.
        private static void Strip()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                // Without the RaidController a death does not send the player to the shelter: the test scene restarts by key.
                if (root.name == "Legacy (old office)" || root.name == "Enemies" || root.name == "RaidController") Object.DestroyImmediate(root);
            }

            var maze = GameObject.Find("Maze");
            for (int i = maze.transform.childCount - 1; i >= 0; i--)
            {
                var child = maze.transform.GetChild(i);
                if (child.name != "Floor" && child.name != "Walls") Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(maze.GetComponent<RaidGenerator>());
            Object.DestroyImmediate(maze.GetComponent<WrapWorld>());
            maze.name = "Arena";

            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            var so = new SerializedObject(spawner);
            // Packs come when asked (F1), not at the start.
            so.FindProperty("packs").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Tile<T>(string path) where T : TileBase => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Paint(Tilemap floor, Tilemap walls)
        {
            floor.ClearAllTiles();
            walls.ClearAllTiles();
            var concrete = Tile<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Stone_01.asset");
            var wood = Tile<TileBase>("Assets/TileSet/Rule Tiles/Floors/Floor-Wood_01.asset");
            var wall = Tile<TileBase>("Assets/TileSet/Rule Tiles/Walls/Wall-Concrete_01.asset");

            // The hall: floor x -20..19, y -12..11, a ring of wall around it.
            for (int x = -20; x <= 19; x++)
                for (int y = -12; y <= 11; y++) floor.SetTile(new Vector3Int(x, y, 0), concrete);
            for (int x = -21; x <= 20; x++)
            {
                walls.SetTile(new Vector3Int(x, 12, 0), wall);
                walls.SetTile(new Vector3Int(x, -13, 0), wall);
            }
            for (int y = -12; y <= 11; y++)
            {
                walls.SetTile(new Vector3Int(-21, y, 0), wall);
                walls.SetTile(new Vector3Int(20, y, 0), wall);
            }

            // Two pillars to fight round.
            Block(walls, wall, -9, 0, -8, 1);
            Block(walls, wall, 8, 0, 9, 1);

            // A partition with a two-wide doorway: a room in the upper left, and a chokepoint for a crowd.
            for (int x = -20; x <= -4; x++)
            {
                if (x == -14 || x == -13) continue;
                walls.SetTile(new Vector3Int(x, 4, 0), wall);
            }
            for (int x = -20; x <= -5; x++)
                for (int y = 5; y <= 11; y++) floor.SetTile(new Vector3Int(x, y, 0), wood);
            for (int y = 5; y <= 11; y++) walls.SetTile(new Vector3Int(-4, y, 0), wall);

            // A two-wide dead-end corridor in the lower right: enemies queue in it, the player can hold it.
            for (int x = 8; x <= 19; x++)
            {
                walls.SetTile(new Vector3Int(x, -6, 0), wall);
                walls.SetTile(new Vector3Int(x, -9, 0), wall);
            }

            floor.CompressBounds();
            walls.CompressBounds();
            RebuildWallCollider(walls.gameObject);
        }

        private static void Block(Tilemap map, TileBase tile, int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++) map.SetTile(new Vector3Int(x, y, 0), tile);
        }

        // The copied composite collider keeps the old outlines; fresh components are made from the new tiles.
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

        private static void Furnish()
        {
            Prop("Desk", FurnitureDir + "Desk-2.png", new Vector2(-12f, 8f), new Vector2(1.8f, 1.5f));
            Prop("Desk", FurnitureDir + "Desk-2.png", new Vector2(-17f, 8f), new Vector2(1.8f, 1.5f));
            Prop("Bookshelf", FurnitureDir + "Tall-Bookshelf.png", new Vector2(-19f, 10.5f), new Vector2(0.9f, 1.6f));
            Prop("Sofa", FurnitureDir + "Big-Sofa.png", new Vector2(0f, 9f), new Vector2(1.5f, 1.3f));
            Prop("Table", FurnitureDir + "Small-Table.png", new Vector2(14f, 6f), new Vector2(0.8f, 0.7f));
            Prop("Table", FurnitureDir + "Small-Table.png", new Vector2(-14f, -4f), new Vector2(0.8f, 0.7f));
            Prop("Plant", FurnitureDir + "Big-Plant.png", new Vector2(18.5f, 10.5f), new Vector2(1f, 1.4f));
            Prop("Plant", FurnitureDir + "Big-Plant.png", new Vector2(-19.5f, -11f), new Vector2(1f, 1.4f));
            Prop("Crate", "Assets/Sprites/Level/Crate.png", new Vector2(4f, -3f), new Vector2(0.9f, 0.9f));
            Prop("Crate", "Assets/Sprites/Level/Box.png", new Vector2(5f, -3f), new Vector2(0.9f, 0.9f));
            Prop("Crate", "Assets/Sprites/Level/Crate.png", new Vector2(-5f, -3f), new Vector2(0.9f, 0.9f));
        }

        private static void Prop(string name, string sprite, Vector2 position, Vector2 colliderSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(props, false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Furniture");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprite);
            renderer.sortingOrder = 5;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = colliderSize;
        }

        // Targets that take a lot and stand still: for reading damage, reach, arc and the weight of a blow.
        private static void Dummies()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Combat/Dokhodyaga.png");
            var material = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TestEnemy.prefab").GetComponent<SpriteRenderer>().sharedMaterial;
            var positions = new[] { new Vector2(-3f, 6f), new Vector2(0f, 6f), new Vector2(3f, 6f), new Vector2(0f, -2f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject("Dummy " + (i + 1)) { layer = LayerMask.NameToLayer("Enemy") };
                go.transform.SetParent(props, false);
                go.transform.position = positions[i];
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.color = new Color(0.75f, 0.8f, 0.95f);
                renderer.sortingOrder = 8;
                go.AddComponent<CircleCollider2D>().radius = 0.34f;

                var health = go.AddComponent<Health>();
                var so = new SerializedObject(health);
                so.FindProperty("maxHealth").floatValue = 300f;
                so.ApplyModifiedPropertiesWithoutUndo();
                go.AddComponent<HitFlash>();
                go.AddComponent<VisionTarget>();
            }
        }

        private static void Pickups()
        {
            PlacePickup("Assets/Prefabs/Weapons/Pipe.prefab", new Vector2(-2f, -10f));
            PlacePickup("Assets/Prefabs/Weapons/Sledgehammer.prefab", new Vector2(2f, -10f));
        }

        private static void PlacePickup(string path, Vector2 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            go.transform.SetParent(props, false);
            go.transform.position = position;
        }

        private static void Tools()
        {
            var go = new GameObject("CombatTestTools");
            var tools = go.AddComponent<CombatTestTools>();
            var so = new SerializedObject(tools);
            var list = so.FindProperty("weapons");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Items/Weapons/Pipe.asset");
            list.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Items/Weapons/Sledgehammer.asset");
            so.FindProperty("spawner").objectReferenceValue = Object.FindFirstObjectByType<EnemySpawner>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}


