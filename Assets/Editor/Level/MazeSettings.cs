using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>Straight corridors that run through the whole level and around it.</summary>
    public enum MazeAvenues
    {
        None = 0,
        /// <summary>One from left to right and one from bottom to top, crossing at the start.</summary>
        Cross = 1,
        /// <summary>One along every border between the regions of the rooms: every room lies in a block of its own.</summary>
        Grid = 2,
    }

    /// <summary>Ready sets of maze settings.</summary>
    public enum MazePreset
    {
        /// <summary>One winding tree with a few loops: long ways around and deep blind branches.</summary>
        Classic,
        /// <summary>The same walls, but many more loops, and dead ends are led on where the walls allow it.</summary>
        Loops,
        /// <summary>As <see cref="Loops"/>, with shorter pieces of wall allowed: fewer dead ends still.</summary>
        LoopsShortWalls,
        /// <summary>Straight avenues between the regions, the maze in the blocks between them, loops.</summary>
        Avenues,
    }

    /// <summary>
    /// The knobs of the corridor maze that <see cref="TemplateBuilder"/> builds: Mayham > Level > Rebuild Corridor
    /// Template reads the asset at <see cref="AssetPath"/>. Change a number, rebuild, walk; the presets in
    /// Mayham > Level > Maze Presets fill the asset with sets of numbers that were compared beforehand.
    /// </summary>
    [CreateAssetMenu(menuName = "Mayham/Maze Settings", fileName = "MazeSettings")]
    public class MazeSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Data/Level/MazeSettings.asset";

        [Tooltip("The same seed always gives the same maze and the same places for the rooms. 0: a new maze on every rebuild.")]
        [SerializeField] private int seed = 4242;

        [Header("Corridors")]
        [Tooltip("How often a passage goes on straight when it can. Higher: longer straight corridors, fewer zigzags.")]
        [SerializeField, Range(0f, 1f)] private float straightBias = 0.55f;

        [Header("Ways around")]
        [Tooltip("Share of the walls between two corridors that are tried for an opening. Higher: more loops, shorter ways, shallower blind branches.")]
        [SerializeField, Range(0f, 1f)] private float loopChance = 0.03f;
        [Tooltip("Share of the dead ends that are led on: straight ahead if the wall there may go, into a neighbouring corridor otherwise.")]
        [SerializeField, Range(0f, 1f)] private float deadEndsLedOn;
        [Tooltip("A wall is only opened when the pieces of wall on both sides of the opening keep at least this many posts. "
            + "Lower: more openings are possible and fewer dead ends are left, but walls that stand on their own get shorter. "
            + "A piece of 6 posts is a wall 20 tiles long.")]
        [SerializeField, Min(2)] private int minWallPiece = 12;

        [Header("Avenues")]
        [Tooltip("Straight corridors through the whole level. The maze grows into the blocks between them from gates in their sides.")]
        [SerializeField] private MazeAvenues avenues = MazeAvenues.None;
        [Tooltip("Cells between two gates from an avenue into the maze next to it, give or take one.")]
        [SerializeField, Min(2)] private int gateSpacing = 5;

        public int Seed => seed;
        public float StraightBias => straightBias;
        public float LoopChance => loopChance;
        public float DeadEndsLedOn => deadEndsLedOn;
        public int MinWallPiece => Mathf.Max(2, minWallPiece);
        public MazeAvenues Avenues => avenues;
        public int GateSpacing => Mathf.Max(2, gateSpacing);

        /// <summary>Fills in the numbers of a preset. The seed is not a part of it and stays as it is.</summary>
        public void Apply(MazePreset preset)
        {
            straightBias = 0.55f;
            gateSpacing = 5;
            switch (preset)
            {
                case MazePreset.Loops:
                    loopChance = 0.35f; deadEndsLedOn = 1f; minWallPiece = 12; avenues = MazeAvenues.None;
                    break;
                case MazePreset.LoopsShortWalls:
                    loopChance = 0.35f; deadEndsLedOn = 1f; minWallPiece = 6; avenues = MazeAvenues.None;
                    break;
                case MazePreset.Avenues:
                    loopChance = 0.2f; deadEndsLedOn = 1f; minWallPiece = 12; avenues = MazeAvenues.Grid;
                    break;
                default:
                    loopChance = 0.03f; deadEndsLedOn = 0f; minWallPiece = 12; avenues = MazeAvenues.None;
                    break;
            }
        }

        /// <summary>Settings that exist only in memory, for trying a preset without touching the asset.</summary>
        public static MazeSettings Temporary(MazePreset preset, int seed)
        {
            var settings = CreateInstance<MazeSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.seed = seed;
            settings.Apply(preset);
            return settings;
        }

        /// <summary>The asset the template builder reads. A project without one gets it with the classic numbers.</summary>
        public static MazeSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MazeSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<MazeSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
