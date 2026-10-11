using System.Linq;
using ProjectMayham.Level;
using ProjectMayham.Quests;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        static partial void TextTests(Checks t)
        {
            t.Eq(QuestText.Pages(null).Count, 0, "no text, no pages");
            t.Eq(QuestText.Pages("  \n \n").Count, 0, "only blanks, no pages");
            var two = QuestText.Pages("Первая страница.\n\nВторая страница.");
            t.Ok(two.Count == 2 && two[0] == "Первая страница." && two[1] == "Вторая страница.", "an empty line starts a new page");
            t.Eq(QuestText.Pages("Строка один.\nСтрока два.").Count, 1, "a single line break stays on the page");

            // The box shows three lines: a fourth one starts a new page even when the text is short.
            t.Eq(QuestText.Pages("Раз.\nДва.\nТри.").Count, 1, "three lines fit on one page");
            var lines = QuestText.Pages("Раз.\nДва.\nТри.\nЧетыре.\nПять.");
            t.Ok(lines.Count == 2 && lines[0] == "Раз.\nДва.\nТри." && lines[1] == "Четыре.\nПять.", "a page holds three lines at most");

            // Review focus 3: Windows line ends and a "blank" line made of spaces.
            var windows = QuestText.Pages("Первая.\r\n   \r\nВторая.\r\n\r\n\r\nТретья.");
            t.Ok(windows.Count == 3 && windows[0] == "Первая." && windows[1] == "Вторая." && windows[2] == "Третья.",
                "CRLF and a line of spaces split pages too");

            string sentence = "Это предложение повторяется, чтобы текст стал длиннее одной страницы. ";
            string longText = string.Concat(Enumerable.Repeat(sentence, 8)).Trim();
            var pages = QuestText.Pages(longText);
            t.Ok(pages.Count >= 2, "a long page is split");
            t.Ok(pages.All(p => p.Length <= QuestText.PageLimit), "no page is longer than the limit");
            t.Ok(pages.Take(pages.Count - 1).All(p => p.EndsWith(".")), "it is split at the end of a sentence");
            t.Eq(string.Join(" ", pages), longText, "nothing is lost");
            var solid = QuestText.Pages(new string('я', QuestText.PageLimit * 2 + 10));
            t.Ok(solid.Count == 3 && solid.All(p => p.Length <= QuestText.PageLimit), "a text without spaces is cut at the limit");
            t.Eq(QuestText.Blocks(longText + "\n\nКоротко.").Count, 2, "the writer's own pages are not cut");

            using (var world = new World())
            {
                var papers = Item("Papers");
                var mug = Item("Mug");
                var quest = world.Quest("Text");
                world.Objective(quest, QuestObjectiveKind.Deliver, papers, 3);
                world.Objective(quest, QuestObjectiveKind.FindSpecial, mug, 1, RoomKind.BossOffice);
                world.Objective(quest, QuestObjectiveKind.VisitRoom, null, 1, RoomKind.Parking);
                world.Reward(quest, 30, mug, 2);

                t.Eq(QuestText.Objectives(quest), "Бумаги ×3; Кружка (Офис босса); Побывать: Парковка", "objectives in one row");
                t.Eq(QuestText.Reward(quest), "30 $, Кружка ×2", "reward");
                t.Eq(QuestText.OfferSummary(quest),
                    "Нужно: Бумаги ×3; Кружка (Офис босса); Побывать: Парковка\nНаграда: 30 $, Кружка ×2", "summary of an offer");

                QuestLog.Accept(quest);
                var backpack = world.Inventory();
                backpack.Add(papers, 2);
                var stash = world.Inventory();
                stash.Add(mug, 1);
                t.Eq(QuestText.Checklist(QuestLog.Check(quest, backpack, stash)),
                    "Бумаги — 2/3; Кружка (Офис босса) — 0/1, в тайнике: 1; Побывать: Парковка — ещё нет", "progress in one row");
                backpack.Add(papers, 5);
                t.Ok(QuestText.Checklist(QuestLog.Check(quest, backpack)).StartsWith("Бумаги — 3/3;"), "more than needed is shown as enough");
                QuestLog.CommitVisits(new[] { RoomKind.Parking });
                t.Ok(QuestText.Checklist(QuestLog.Check(quest, backpack)).EndsWith("Побывать: Парковка — есть"), "a place that was brought home");

                var free = world.Quest("Free");
                t.Eq(QuestText.Reward(free), string.Empty, "no reward, no text");
                t.Eq(QuestText.OfferSummary(free), string.Empty, "nothing asked and nothing paid: no summary");
                t.Eq(QuestText.Small("x"), "<size=80%>x</size>", "small print");
            }
        }
    }
}
