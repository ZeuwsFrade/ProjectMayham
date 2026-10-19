using ProjectMayham.Player;
using ProjectMayham.Quests;
using UnityEditor;

namespace ProjectMayham.EditorTools
{
    public static partial class QuestSelfTest
    {
        static partial void SkillTests(Checks t)
        {
            var coin = Item("Coin");

            using (var world = new World())
            {
                var trick = world.Quest("Trick");
                world.Objective(trick, QuestObjectiveKind.Deliver, coin, 2);
                world.Set(trick, "rewardSkill", PlayerSkills.VendingShake);
                t.Ok(QuestText.Reward(trick).Contains("навык «Трясти автоматы»"), "the reward names the skill");
                t.Ok(!PlayerSkills.Has(PlayerSkills.VendingShake), "the skill is not known before");

                QuestLog.Accept(trick);
                var backpack = world.Inventory();
                backpack.Add(coin, 2);
                t.Eq(QuestLog.TryTurnIn(trick, backpack), TurnInResult.Done, "the quest is handed in");
                t.Ok(PlayerSkills.Has(PlayerSkills.VendingShake), "and the skill is learned");
                PlayerSkills.Learn(PlayerSkills.VendingShake);
                t.Eq(world.Data.skills.Count, 1, "learned once");

                var typo = world.Quest("Typo", 1);
                world.Objective(typo, QuestObjectiveKind.Deliver, coin, 1);
                world.Set(typo, "rewardSkill", "vending_shak");
                t.Ok(Errors(world, TestFacts(coin)).Contains("навыка «vending_shak» нет"), "a skill nobody knows is an error");
            }
        }
    }
}
