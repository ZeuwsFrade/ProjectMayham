using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.Level;
using ProjectMayham.Npcs;

namespace ProjectMayham.Quests
{
    public enum QuestStatus
    {
        /// <summary>A quest it depends on has not been turned in.</summary>
        Locked,
        /// <summary>Can be taken.</summary>
        Available,
        /// <summary>Taken, not turned in yet.</summary>
        Active,
        TurnedIn
    }

    public enum TurnInResult
    {
        Done,
        /// <summary>The quest is not taken (or already turned in).</summary>
        NotActive,
        /// <summary>Something the quest asks for is missing.</summary>
        NotReady,
        /// <summary>The reward does not fit into the backpack; nothing was changed.</summary>
        NoRoom
    }

    /// <summary>How far one objective is.</summary>
    public class ObjectiveProgress
    {
        public QuestObjective Objective;
        /// <summary>Pieces in the backpack and the hands; for a place: 1 once it was reached and brought home.</summary>
        public int Have;
        public int Need;
        /// <summary>Pieces lying in the stash: they do not count until the player carries them.</summary>
        public int InStash;
        public bool Done => Have >= Need;
    }

    /// <summary>How far a quest is, objective by objective.</summary>
    public class QuestProgress
    {
        public readonly List<ObjectiveProgress> Lines = new List<ObjectiveProgress>();
        public bool Ready => Lines.All(line => line.Done);
    }

    /// <summary>
    /// The rules of quests: which state a quest is in, taking it, checking it, turning it in, what the active
    /// quests ask of the next raid. Keeps no state of its own: every call reads the quest list of the game
    /// session, so a new game or a loaded one needs no extra step.
    /// </summary>
    public static class QuestLog
    {
        private const int StateActive = 1;
        private const int StateTurnedIn = 2;
        private const int StateAbandoned = 3;

        // A save from before quests has no list.
        private static List<QuestProgressData> Records
        {
            get
            {
                var data = GameSession.Data;
                return data.quests ??= new List<QuestProgressData>();
            }
        }

        // The first entry of the quest that means something; later ones and unknown states are ignored.
        private static QuestProgressData Record(QuestDefinition quest)
        {
            foreach (var record in Records)
            {
                if (record == null || record.quest != quest.Id) continue;
                if (record.state == StateActive || record.state == StateTurnedIn || record.state == StateAbandoned) return record;
            }
            return null;
        }

        // Turned in at least once: a daily that was taken again still counts for the quests that require it.
        private static bool IsTurnedIn(QuestDefinition quest)
        {
            var record = Record(quest);
            return record != null && (record.state == StateTurnedIn || record.done > 0);
        }

        /// <summary>The day of the dailies: the number of raids the player has set out on.</summary>
        public static int Day => GameSession.Data.raids;

        /// <summary>A raid begins: a new day, the givers pick new dailies.</summary>
        public static void CountRaid() => GameSession.Data.raids++;

        public static QuestStatus StatusOf(QuestDefinition quest)
        {
            var record = Record(quest);
            if (record != null && record.state == StateActive) return QuestStatus.Active;
            if (record != null && record.state == StateTurnedIn && !quest.Repeatable) return QuestStatus.TurnedIn;
            if (!Unlocked(quest)) return QuestStatus.Locked;

            // A daily after it was done, or any quest after it was refused, waits for its day.
            bool doneBefore = record != null && (record.state == StateTurnedIn || record.done > 0);
            if (record != null && record.again > Day) return doneBefore ? QuestStatus.TurnedIn : QuestStatus.Locked;
            if (quest.Repeatable && !DailyPicks(quest.Giver).Contains(quest.Id)) return doneBefore ? QuestStatus.TurnedIn : QuestStatus.Locked;
            return QuestStatus.Available;
        }

        // What the content asks before a quest can be offered.
        private static bool Unlocked(QuestDefinition quest)
        {
            // Only the entries of the required quests are looked at, never their own requirements:
            // a cycle in the content locks its quests instead of hanging the game.
            foreach (var required in quest.Requires)
            {
                if (required != null && !IsTurnedIn(required)) return false;
            }
            // The giver has to trust the player enough, and the answers the quest line forks on have to be the right ones.
            if (quest.Giver != null && Loyalty.Level(quest.Giver.Id) < quest.MinLoyalty) return false;
            if (quest.RequiredChoices.Any(c => !string.IsNullOrWhiteSpace(c) && !PlayerChoices.Has(c))) return false;
            if (quest.ExcludingChoices.Any(PlayerChoices.Has)) return false;
            return true;
        }

        private static bool IsActive(QuestDefinition quest)
        {
            var record = Record(quest);
            return record != null && record.state == StateActive;
        }

        /// <summary>
        /// The dailies a giver offers today. Picked once a day at random among those that can be offered (unlocked,
        /// not taken, not waiting after being done or refused), as many as the giver has free slots for — the
        /// dailies taken and not done take slots. Kept in the save, so taking or turning one in changes nothing else.
        /// </summary>
        public static List<string> DailyPicks(QuestGiverDefinition giver)
        {
            if (giver == null) return new List<string>();
            var data = GameSession.Data;
            data.dailies ??= new List<DailyOfferData>();
            var entry = data.dailies.Find(d => d != null && d.giver == giver.Id);
            if (entry != null && entry.day == Day && entry.quests != null) return entry.quests;

            var own = QuestCatalog.ByGiver(giver.Id).Where(q => q.Repeatable).ToList();
            var pool = own.Where(q => !IsActive(q) && Unlocked(q) && (Record(q) == null || Record(q).again <= Day)).ToList();
            int slots = System.Math.Max(0, giver.DailySlots - own.Count(IsActive));
            var dice = new System.Random(unchecked(Day * 7919 + StableHash(giver.Id)));
            var picks = pool.OrderBy(_ => dice.Next()).Take(slots).Select(q => q.Id).ToList();

            if (entry == null)
            {
                entry = new DailyOfferData { giver = giver.Id };
                data.dailies.Add(entry);
            }
            entry.day = Day;
            entry.quests = picks;
            return picks;
        }

        // The same number for the same id in every run (string.GetHashCode is not promised to be).
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return hash;
            }
        }

        /// <summary>Quests this NPC can give right now.</summary>
        public static List<QuestDefinition> Offers(string npcId) =>
            QuestCatalog.ByGiver(npcId).Where(q => StatusOf(q) == QuestStatus.Available).ToList();

        /// <summary>Quests of this NPC the player has taken and not turned in.</summary>
        public static List<QuestDefinition> ActiveOf(string npcId) =>
            QuestCatalog.ByGiver(npcId).Where(q => StatusOf(q) == QuestStatus.Active).ToList();

        /// <summary>Takes a quest that is on offer. False (and nothing changes) in any other state.</summary>
        public static bool Accept(QuestDefinition quest)
        {
            if (quest == null || StatusOf(quest) != QuestStatus.Available) return false;
            // A daily or a refused quest taken again starts from scratch in its old entry.
            var record = Record(quest);
            if (record != null)
            {
                record.state = StateActive;
                record.marks = new List<string>();
            }
            else Records.Add(new QuestProgressData { quest = quest.Id, state = StateActive });
            return true;
        }

        /// <summary>True when the player can refuse this quest now: it is taken and allowed to be refused (never a rescue).</summary>
        public static bool CanAbandon(QuestDefinition quest) =>
            quest != null && quest.CanAbandon && !quest.IsRescue && IsActive(quest);

        /// <summary>
        /// The player refuses a quest they have taken: the giver takes off loyalty, what was reached for it is
        /// forgotten, things found for it stay with the player. The quest is offered again from the next day (a
        /// daily after its own wait). False (and nothing changes) when it cannot be refused.
        /// </summary>
        public static bool Abandon(QuestDefinition quest)
        {
            if (!CanAbandon(quest)) return false;
            var record = Record(quest);
            record.state = record.done > 0 ? StateTurnedIn : StateAbandoned;
            record.marks = new List<string>();
            record.again = Day + (quest.Repeatable ? quest.RepeatAfterRaids : 1);
            if (quest.Giver != null) Loyalty.Add(quest.Giver.Id, -quest.AbandonPenalty);
            return true;
        }

        /// <summary>Pieces of an item the player carries: in the grid and in the hands (CountOf leaves the hands out).</summary>
        public static int Carried(GridInventory backpack, ItemDefinition item)
        {
            if (backpack == null || item == null) return 0;
            return backpack.CountOf(item) + backpack.HeldCount(item);
        }

        /// <summary>
        /// How far the quest is with what the player carries. Only the backpack and the hands count; with a
        /// <paramref name="stash"/> the result also says how many pieces lie there, so the giver can mention it.
        /// </summary>
        public static QuestProgress Check(QuestDefinition quest, GridInventory backpack, GridInventory stash = null)
        {
            var progress = new QuestProgress();
            var record = Record(quest);
            foreach (var objective in quest.Objectives)
            {
                var line = new ObjectiveProgress { Objective = objective };
                if (objective.Kind == QuestObjectiveKind.VisitRoom)
                {
                    line.Need = 1;
                    line.Have = record != null && record.marks != null && record.marks.Contains(objective.VisitMark) ? 1 : 0;
                }
                else if (objective.Kind == QuestObjectiveKind.Rescue)
                {
                    line.Need = 1;
                    line.Have = quest.Giver != null && NpcRescue.IsRescued(quest.Giver.Id) ? 1 : 0;
                }
                else
                {
                    line.Need = objective.Count;
                    line.Have = Carried(backpack, objective.Item);
                    line.InStash = stash != null ? stash.CountOf(objective.Item) : 0;
                }
                progress.Lines.Add(line);
            }
            return progress;
        }

        /// <summary>
        /// Hands the quest in: all or nothing, like a barter. The items are taken from the backpack (and the hands),
        /// the reward is put in; when the reward does not fit the backpack is restored exactly and nothing is paid.
        /// The file is written by the next regular save, as with trading.
        /// </summary>
        public static TurnInResult TryTurnIn(QuestDefinition quest, GridInventory backpack)
        {
            var record = quest != null ? Record(quest) : null;
            if (record == null || record.state != StateActive) return TurnInResult.NotActive;
            if (backpack == null || !Check(quest, backpack).Ready) return TurnInResult.NotReady;

            var snapshot = backpack.Export();
            foreach (var objective in quest.Objectives)
            {
                if (!objective.NeedsItem) continue;

                int left = objective.Count - backpack.RemoveItems(objective.Item, objective.Count);
                while (left > 0 && backpack.TakeFromHands(objective.Item)) left--;
                if (left <= 0) continue;

                // Two objectives of one quest asked for the same item (the validator forbids it).
                backpack.Import(snapshot);
                return TurnInResult.NotReady;
            }

            foreach (var reward in quest.RewardItems)
            {
                if (reward == null || reward.item == null) continue;
                // An amount left at 0 in the Inspector means one piece, the same as for objectives.
                if (backpack.Add(reward.item, System.Math.Max(1, reward.count)) <= 0) continue;

                backpack.Import(snapshot);
                return TurnInResult.NoRoom;
            }

            GameSession.AddMoney(quest.RewardMoney);
            if (quest.Giver != null) Loyalty.Add(quest.Giver.Id, quest.RewardLoyalty);
            Player.PlayerSkills.Learn(quest.RewardSkill);
            Shelter.Blueprints.Learn(quest.RewardBlueprint);
            record.state = StateTurnedIn;
            record.done++;
            if (quest.Repeatable) record.again = Day + quest.RepeatAfterRaids;
            return TurnInResult.Done;
        }

        /// <summary>
        /// Marks the kinds of rooms the player reached in a raid and came back from, for every active quest that
        /// asks for them. Marking twice changes nothing.
        /// </summary>
        public static void CommitVisits(IEnumerable<RoomKind> visited)
        {
            var kinds = new HashSet<RoomKind>(visited);
            if (kinds.Count == 0) return;

            foreach (var quest in QuestCatalog.All)
            {
                var record = Record(quest);
                if (record == null || record.state != StateActive) continue;

                foreach (var objective in quest.Objectives)
                {
                    if (objective.Kind != QuestObjectiveKind.VisitRoom || !kinds.Contains(objective.Room)) continue;
                    record.marks ??= new List<string>();
                    if (!record.marks.Contains(objective.VisitMark)) record.marks.Add(objective.VisitMark);
                }
            }
        }

        /// <summary>
        /// What the active quests ask of the next raid: rooms to reach, and special items the player does not own
        /// yet (in the saved backpack, hands and stash). Call it after the game was saved on leaving the shelter.
        /// </summary>
        public static RaidDemands BuildRaidDemands()
        {
            var demands = new RaidDemands();
            var data = GameSession.Data;

            // In the order the quests were taken: when rooms run short the oldest quest is served first.
            foreach (var record in Records.ToList())
            {
                if (record == null || record.state != StateActive) continue;
                var quest = QuestCatalog.Find(record.quest);
                if (quest == null || Record(quest) != record) continue;   // an unknown quest, or a second entry

                foreach (var objective in quest.Objectives)
                {
                    if (objective.Kind == QuestObjectiveKind.VisitRoom)
                    {
                        if (record.marks == null || !record.marks.Contains(objective.VisitMark)) demands.AddVisit(objective.Room);
                    }
                    else if (objective.Kind == QuestObjectiveKind.FindSpecial)
                    {
                        int missing = objective.Count - Owned(data, objective.Item);
                        if (missing > 0) demands.AddItem(objective.Item, missing, objective.Room, objective.Hidden);
                    }
                }
            }
            return demands;
        }

        /// <summary>How many pieces of an item a save holds: carried (the hands too) and in the stash.</summary>
        public static int Owned(SaveData data, ItemDefinition item) => CountIn(data.player, item) + CountIn(data.stash, item);

        private static int CountIn(InventoryData inventory, ItemDefinition item)
        {
            if (inventory == null) return 0;

            int total = (inventory.held == item.Id ? 1 : 0) + (inventory.heldLeft == item.Id ? 1 : 0);
            if (inventory.stacks == null) return total;
            foreach (var stack in inventory.stacks)
            {
                if (stack != null && stack.item == item.Id) total += stack.count;
            }
            return total;
        }
    }
}
