using System.Collections.Generic;
using ProjectMayham.Core;
using ProjectMayham.Level;
using ProjectMayham.Player;
using ProjectMayham.UI;
using ProjectMayham.World;
using TMPro;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// Watches where the player goes in a raid: remembers every kind of room they stood in and shows a short note
    /// when it is one a quest asks for. What was reached is handed to the quests only when the player comes back:
    /// the game is saved in a raid only on leaving it (<see cref="SceneLoader.Load"/>), so "a save while the
    /// player is alive" means "came home". A death, or quitting in the middle of the raid, hands over nothing.
    /// Added by <see cref="RaidGenerator"/> when a raid starts; nothing is placed in a scene.
    /// </summary>
    public class RaidQuestTracker : MonoBehaviour
    {
        // Tiles the player has to be inside the room, away from its walls: standing in the doorway is not enough.
        private const float WallInset = 1f;
        private const float NoticeSeconds = 2.5f;

        private readonly HashSet<RoomKind> visited = new HashSet<RoomKind>();
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

        private void OnEnable() => GameSession.SaveRequested += Commit;
        private void OnDisable() => GameSession.SaveRequested -= Commit;

        private void Update()
        {
            // Unscaled: the note also goes away while a window has paused the game.
            if (notice != null && notice.gameObject.activeSelf && Time.unscaledTime >= noticeUntil) notice.gameObject.SetActive(false);

            if (generator == null) return;
            if (player == null) player = FindFirstObjectByType<PlayerStats>();
            if (player == null || player.IsDead) return;

            Vector2 position = player.transform.position;
            foreach (var room in generator.Rooms)
            {
                if (visited.Contains(room.Kind) || !IsInside(room.Slot, position)) continue;

                visited.Add(room.Kind);
                if (demands != null && demands.WantsVisit(room.Kind)) Show($"Отмечено: {RoomSizes.Title(room.Kind)}");
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

        private void Commit()
        {
            // No player or a dead one: what was reached in this raid is not brought home.
            if (player == null || player.IsDead) return;
            QuestLog.CommitVisits(visited);
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
