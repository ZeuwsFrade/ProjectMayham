using ProjectMayham.Localization;
using System.Collections.Generic;
using ProjectMayham.Combat;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.World;
using TMPro;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// Stages raid objectives in memory. Only successful extraction hands them over to the quest log.
    /// </summary>
    public class RaidQuestTracker : MonoBehaviour
    {
        // Tiles the player has to be inside the room, away from its walls: standing in the doorway is not enough.
        private const float WallInset = 1f;
        private const float NoticeSeconds = 2.5f;

        private readonly HashSet<RoomKind> visited = new HashSet<RoomKind>();
        private readonly HashSet<RaidZone> zones = new HashSet<RaidZone>();
        private int kills;
        private bool committed;
        private RaidGenerator generator;
        private RaidDemands demands;
        private PlayerStats player;
        private TMP_Text notice;
        private float noticeUntil;

        /// <summary>Every kind of room the player has stood in during this raid.</summary>
        public IReadOnlyCollection<RoomKind> Visited => visited;
        /// <summary>The note on screen right now, or empty.</summary>
        public string Notice => notice != null && notice.gameObject.activeSelf ? notice.text : string.Empty;

        public void Begin(RaidGenerator source, RaidDemands wanted)
        {
            generator = source;
            demands = wanted;
        }

        private void OnEnable() => CombatEvents.EnemyKilledByPlayer += OnEnemyKilled;
        private void OnDisable() => CombatEvents.EnemyKilledByPlayer -= OnEnemyKilled;

        private void OnEnemyKilled(GameObject enemy)
        {
            if (!committed && generator != null && enemy != null && enemy.scene == gameObject.scene) kills++;
        }

        private void Update()
        {
            // Unscaled: the note also goes away while a window has paused the game.
            if (notice != null && notice.gameObject.activeSelf && Time.unscaledTime >= noticeUntil) notice.gameObject.SetActive(false);

            if (generator == null) return;
            if (player == null) player = FindFirstObjectByType<PlayerStats>();
            if (player == null || player.IsDead) return;

            Vector2 position = player.transform.position;
            zones.Add(generator.ZoneAt(position));
            foreach (var room in generator.Rooms)
            {
                if (visited.Contains(room.Kind) || !IsInside(room.Slot, position)) continue;

                visited.Add(room.Kind);
                if (demands != null && demands.WantsVisit(room.Kind)) Show(GameText.Format("Отмечено: {0}", RoomSizes.Title(room.Kind)));
            }
        }

        /// <summary>
        /// True when the point is inside the room of the slot, a tile away from its walls. The level wraps around,
        /// so the distance to the middle of the room is taken the short way.
        /// </summary>
        public static bool IsInside(RoomSlot slot, Vector2 point)
        {
            Vector2 half = (Vector2)slot.Interior * 0.5f;
            Vector2 centre = (Vector2)slot.InteriorOrigin + half;
            var wrap = WrapWorld.Active;
            Vector2 delta = wrap != null ? wrap.Delta(centre, point) : point - centre;
            return Mathf.Abs(delta.x) <= half.x - WallInset && Mathf.Abs(delta.y) <= half.y - WallInset;
        }

        public void CommitExtraction()
        {
            if (committed || player == null || player.IsDead) return;

            committed = true;
            QuestLog.CommitRaid(visited, zones, kills);
        }

        private void Show(string text)
        {
            if (notice == null)
            {
                var canvas = UIKit.NewCanvas("QuestNotice", 50, transform);
                notice = UIKit.Label(canvas.transform, string.Empty, 34f, TextAlignmentOptions.Center, UIKit.Accent);
                UIKit.Place(notice.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 60f));
                notice.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            notice.text = text;
            notice.gameObject.SetActive(true);
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }
    }
}
