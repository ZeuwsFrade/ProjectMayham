using System.Collections.Generic;
using ProjectMayham.Core;
using UnityEngine;

namespace ProjectMayham.Combat
{
    /// <summary>
    /// How much of its durability a weapon has left. The inventory knows items by their definition only, so wear is
    /// kept per kind of weapon: two pipes in the backpack share one durability, and a new pipe found after one broke
    /// is whole again. It lives in <see cref="GameSession.Data"/>, so it survives the trip between shelter and raid.
    /// </summary>
    public static class WeaponWear
    {
        /// <summary>Durability left; the full value for a weapon that was never used.</summary>
        public static float Get(WeaponDefinition weapon)
        {
            if (weapon == null || !weapon.Wears) return float.PositiveInfinity;

            var entry = Find(weapon);
            return entry != null ? Mathf.Clamp(entry.durability, 0f, weapon.MaxDurability) : weapon.MaxDurability;
        }

        /// <summary>Share of the durability that is left, 0..1 (1 for a weapon that does not wear out).</summary>
        public static float Fraction(WeaponDefinition weapon)
        {
            if (weapon == null || !weapon.Wears) return 1f;
            return Mathf.Clamp01(Get(weapon) / weapon.MaxDurability);
        }

        /// <summary>Takes <paramref name="amount"/> off the durability. True when nothing is left: the weapon breaks.</summary>
        public static bool Wear(WeaponDefinition weapon, float amount)
        {
            if (weapon == null || !weapon.Wears || amount <= 0f) return false;

            float left = Mathf.Max(0f, Get(weapon) - amount);
            if (left <= 0f)
            {
                Forget(weapon);
                return true;
            }
            Set(weapon, left);
            return false;
        }

        /// <summary>Makes the weapon whole again (repair, or a new one).</summary>
        public static void Forget(WeaponDefinition weapon)
        {
            if (weapon == null) return;
            GameSession.Data.weaponWear.RemoveAll(e => e.item == weapon.Id);
        }

        /// <summary>
        /// Makes every weapon whole again except those whose ids are given (the ones that are still owned): a weapon
        /// that was lost has nothing left to remember.
        /// </summary>
        public static void ForgetExcept(IEnumerable<string> owned)
        {
            var keep = new HashSet<string>(owned ?? System.Array.Empty<string>());
            GameSession.Data.weaponWear.RemoveAll(e => !keep.Contains(e.item));
        }

        private static void Set(WeaponDefinition weapon, float durability)
        {
            var entry = Find(weapon);
            if (entry == null)
            {
                entry = new WeaponWearData { item = weapon.Id };
                GameSession.Data.weaponWear.Add(entry);
            }
            entry.durability = durability;
        }

        private static WeaponWearData Find(WeaponDefinition weapon) =>
            GameSession.Data.weaponWear.Find(e => e.item == weapon.Id);
    }
}
