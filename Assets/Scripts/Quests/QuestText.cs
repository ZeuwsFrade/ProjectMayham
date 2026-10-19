using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectMayham.Quests
{
    /// <summary>
    /// The wording around quests: cuts what the writer wrote into pages that fit the dialogue window and puts
    /// together the lines the game writes itself (what is asked, what is paid, how far the player is).
    /// </summary>
    public static class QuestText
    {
        /// <summary>Characters that fit into the text box of the dialogue window (about three lines).</summary>
        public const int PageLimit = 230;
        /// <summary>Lines the text box of the dialogue window shows; what goes beyond is cut off by the window.</summary>
        public const int PageLines = 3;

        // An empty line, or one that holds only blanks, ends a page.
        private static readonly Regex PageBreak = new Regex(@"\n[ \t]*(?:\n[ \t]*)+", RegexOptions.Compiled);

        /// <summary>The pages as the writer marked them with empty lines, before anything is cut to fit.</summary>
        public static List<string> Blocks(string text)
        {
            var blocks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return blocks;

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (string block in PageBreak.Split(normalized))
            {
                string trimmed = block.Trim();
                if (trimmed.Length > 0) blocks.Add(trimmed);
            }
            return blocks;
        }

        /// <summary>
        /// The pages of a text as the window shows them: the writer's own pages, each cut further when it has more
        /// lines than the box shows or is too long (after the last sentence that fits, or at the last space, or
        /// at the limit).
        /// </summary>
        public static List<string> Pages(string text)
        {
            var pages = new List<string>();
            foreach (string block in Blocks(text))
            {
                foreach (string part in ByLines(block))
                {
                    string rest = part;
                    while (rest.Length > PageLimit)
                    {
                        int cut = CutPoint(rest);
                        pages.Add(rest.Substring(0, cut).TrimEnd());
                        rest = rest.Substring(cut).TrimStart();
                    }
                    if (rest.Length > 0) pages.Add(rest);
                }
            }
            return pages;
        }

        /// <summary>Lines of a page as the writer broke them.</summary>
        public static int LineCount(string page) => string.IsNullOrEmpty(page) ? 0 : page.Split('\n').Length;

        // A page with more line breaks than the box has lines goes on after every third line.
        private static IEnumerable<string> ByLines(string block)
        {
            var lines = block.Split('\n');
            for (int i = 0; i < lines.Length; i += PageLines)
            {
                yield return string.Join("\n", lines, i, Mathf.Min(PageLines, lines.Length - i)).Trim();
            }
        }

        private static int CutPoint(string text)
        {
            int sentenceEnd = -1;
            for (int i = 0; i < PageLimit; i++)
            {
                char c = text[i];
                bool ends = c == '.' || c == '!' || c == '?' || c == '…';
                if (ends && char.IsWhiteSpace(text[i + 1])) sentenceEnd = i + 1;
            }
            if (sentenceEnd > 0) return sentenceEnd;

            int space = text.LastIndexOf(' ', PageLimit - 1);
            return space > 0 ? space : PageLimit;
        }

        /// <summary>What the quest asks for in one row: "Бумаги ×3; Красная папка (Офис босса)".</summary>
        public static string Objectives(QuestDefinition quest) =>
            string.Join("; ", quest.Objectives.Select(objective => objective.Describe()));

        /// <summary>What the quest pays: "30 $, Кружка ×2". Empty when it pays nothing. Loyalty is hidden from the player.</summary>
        public static string Reward(QuestDefinition quest)
        {
            var parts = new List<string>();
            if (quest.RewardMoney > 0) parts.Add($"{quest.RewardMoney} $");
            foreach (var reward in quest.RewardItems)
            {
                if (reward == null || reward.item == null) continue;
                parts.Add(reward.count > 1 ? $"{reward.item.DisplayName} ×{reward.count}" : reward.item.DisplayName);
            }
            if (quest.RewardSkill.Length > 0) parts.Add($"навык «{Player.PlayerSkills.Title(quest.RewardSkill)}»");
            return string.Join(", ", parts);
        }

        /// <summary>The page the game puts under an offer: what is asked and what is paid, a line each.</summary>
        public static string OfferSummary(QuestDefinition quest)
        {
            var lines = new List<string>();
            if (quest.Objectives.Count > 0) lines.Add($"Нужно: {Objectives(quest)}");
            string reward = Reward(quest);
            if (reward.Length > 0) lines.Add($"Награда: {reward}");
            return string.Join("\n", lines);
        }

        /// <summary>How far the player is, in one row: "Бумаги — 2/3; Побывать: Парковка — ещё нет".</summary>
        public static string Checklist(QuestProgress progress) => string.Join("; ", progress.Lines.Select(Line));

        private static string Line(ObjectiveProgress line)
        {
            string subject = line.Objective.Subject;
            if (line.Objective.Kind == QuestObjectiveKind.VisitRoom) return $"{subject} — {(line.Done ? "есть" : "ещё нет")}";
            if (line.Objective.Kind == QuestObjectiveKind.Rescue) return $"{subject} — {(line.Done ? "готово" : "ещё нет")}";

            string text = $"{subject} — {Mathf.Min(line.Have, line.Need)}/{line.Need}";
            if (!line.Done && line.InStash > 0) text += $", в тайнике: {line.InStash}";
            return text;
        }

        /// <summary>Lines the game writes itself are set smaller than what the NPC says.</summary>
        public static string Small(string text) => $"<size=80%>{text}</size>";
    }
}
