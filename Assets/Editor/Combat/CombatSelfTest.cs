using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectMayham.AI;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Items;
using ProjectMayham.World;
using UnityEditor;
using UnityEngine;

namespace ProjectMayham.EditorTools
{
    /// <summary>
    /// Checks of the fighting that run in the editor without Play mode: Mayham > Combat > Run Self-Test.
    /// The weapons and the data of the shipped assets, the wear of weapons, damage and health, the alert level, the
    /// freeze of a hit against a pause, and the path search on grids built in memory (open ground, a wall with a gap,
    /// a sealed target, a level that wraps around).
    /// </summary>
    public static class CombatSelfTest
    {
        [MenuItem("Mayham/Combat/Run Self-Test")]
        public static void RunMenu() => Debug.Log(Run());

        /// <summary>Runs every group. "PASS: N checks", or "FAIL" with one line per failed check.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "FAIL: leave Play mode first";

            var t = new QuestSelfTest.Checks();
            Group(t, "assets", () => AssetTests(t));
            Group(t, "loners", () => LonerTests(t));
            Group(t, "wear", () => WearTests(t));
            Group(t, "health", () => HealthTests(t));
            Group(t, "alert", () => AlertTests(t));
            Group(t, "time", () => TimeTests(t));
            Group(t, "paths", () => PathTests(t));

            return t.Failures.Count == 0
                ? $"PASS: {t.Count} checks"
                : $"FAIL: {t.Failures.Count} of {t.Count} checks\n - " + string.Join("\n - ", t.Failures);
        }

        private static void Group(QuestSelfTest.Checks t, string name, Action run)
        {
            t.Group = name;
            try
            {
                run();
            }
            catch (Exception e)
            {
                t.Count++;
                t.Failures.Add($"[{name}] exception: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        // ---- the assets that ship ----

        private static void AssetTests(QuestSelfTest.Checks t)
        {
            var pipe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Items/Weapons/Pipe.asset");
            var hammer = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Items/Weapons/Sledgehammer.asset");
            t.Ok(pipe != null && hammer != null, "both test weapons exist (Mayham/Combat/1)");
            if (pipe == null || hammer == null) return;

            foreach (var weapon in new[] { pipe, hammer })
            {
                string id = weapon.Id;
                t.Ok(weapon.Holdable, $"{id} can be taken in the hands");
                t.Ok(weapon.WorldPrefab != null && weapon.WorldPrefab.GetComponent<ItemPickup>() != null
                     && weapon.WorldPrefab.GetComponent<ItemPickup>().Item == weapon, $"{id} has a pickup prefab that holds it");
                t.Ok(ItemDatabase.Find(id) == weapon, $"{id} is in the item database");
                t.Ok(weapon.Damage > 0f && weapon.HeavyDamage > weapon.Damage, $"{id}: the charged blow hurts more");
                t.Ok(weapon.HeavyStaminaCost > weapon.StaminaCost && weapon.StaminaCost > 0f, $"{id}: the charged blow costs more stamina");
                t.Ok(weapon.HeavySwingSeconds >= weapon.SwingSeconds, $"{id}: the charged blow is not quicker");
                t.Ok(weapon.HeavyKnockback > weapon.Knockback && weapon.HeavyStagger > weapon.Stagger, $"{id}: the charged blow shoves and staggers more");
                t.Ok(weapon.HeavyArc >= weapon.Arc && weapon.Arc >= 20f && weapon.HeavyArc <= 360f, $"{id}: the arc is sane");
                t.Ok(weapon.Wears && weapon.MaxDurability > 0f, $"{id} wears out");
                t.Ok(weapon.RaisedSpeedMultiplier < 1f && weapon.RaisedConeScale < 1f, $"{id}: raised, it slows the player and narrows the view");
            }

            // The two are meant to feel different: light and quick against heavy and slow.
            t.Ok(pipe.SwingSeconds < hammer.SwingSeconds, "the pipe swings faster than the sledgehammer");
            t.Ok(pipe.Damage < hammer.Damage, "the pipe hurts less than the sledgehammer");
            t.Ok(pipe.StaminaCost < hammer.StaminaCost, "the pipe is cheaper to swing");
            t.Ok(pipe.Range < hammer.Range, "the sledgehammer reaches farther");
            t.Ok(pipe.HitStop <= hammer.HitStop, "the sledgehammer's blow freezes the game for longer");

            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Dokhodyaga.asset");
            t.Ok(enemy != null, "the Dokhodyaga definition exists");
            if (enemy != null)
            {
                // Weak alone: it is slower than a walking player (3.5) and dies in a few blows.
                t.Ok(enemy.ChaseSpeed < 3.5f, "the Dokhodyaga is slower than a walking player");
                t.Ok(enemy.MaxHealth <= pipe.HeavyDamage * 2f, "two charged blows of the pipe kill a Dokhodyaga");
                t.Ok(enemy.Damage * 10f < 100f, "one Dokhodyaga cannot kill the player in ten blows");
                t.Ok(enemy.Shouts && enemy.SurroundRange > enemy.AttackRange, "it calls the pack and goes round the player");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Dokhodyaga.prefab");
            t.Ok(prefab != null, "the Dokhodyaga prefab exists");
            if (prefab != null)
            {
                t.Ok(prefab.layer == LayerMask.NameToLayer("Enemy"), "the Dokhodyaga is on the Enemy layer");
                t.Ok(prefab.GetComponent<Dokhodyaga>() != null && prefab.GetComponent<Health>() != null
                     && prefab.GetComponent<EnemyMotor>() != null && prefab.GetComponent<WrapEntity>() != null, "the Dokhodyaga has its parts");
                t.Ok(prefab.GetComponent<CircleCollider2D>() != null && !prefab.GetComponent<CircleCollider2D>().isTrigger, "the Dokhodyaga has a body that blocks");
            }
        }

        // ---- the Ram and Justice ----

        private static void LonerTests(QuestSelfTest.Checks t)
        {
            var pipe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Items/Weapons/Pipe.asset");
            var ram = AssetDatabase.LoadAssetAtPath<RamDefinition>("Assets/Data/Enemies/Ram.asset");
            var justice = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Justice.asset");
            t.Ok(ram != null, "the Ram definition exists and is a RamDefinition (Mayham/Combat/1b)");
            t.Ok(justice != null, "the Justice definition exists");

            if (ram != null)
            {
                t.Ok(ram.Hearing <= 0f && !ram.HearsEnemies, "the Ram is deaf");
                t.Ok(!ram.Shouts, "the Ram calls nobody");
                t.Ok(ram.SightRange >= 8f && ram.SightAngle < 120f, "the Ram sees far down a narrow cone");
                t.Ok(ram.AttackRange <= ram.SightRange, "the Ram aims only at what it can see");
                t.Ok(ram.ChargeSpeed > 6f, "a charging Ram is faster than a sprinting player (6)");
                t.Ok(ram.WindupSeconds >= 0.6f, "the Ram aims long enough to be dodged");
                t.Ok(ram.TurnSpeed < 180f, "the Ram turns slowly while aiming: a quick player leaves its view");
                t.Ok(ram.StunSeconds >= 1.5f, "a Ram that hits a wall stands dazed long enough to be hit");
                t.Ok(ram.ChargeDamage > 0f && ram.ChargeDamage < 50f, "being run down hurts but does not kill outright");
                t.Ok(ram.TrampleDamage > 0f && ram.ChargeKnockback > 0f, "the Ram tramples the enemies in its way");
            }

            if (justice != null && pipe != null)
            {
                t.Ok(justice.SightRange <= justice.SenseRange + 0.01f && justice.SightRange <= 2.5f, "Justice is blind: it finds by touch only");
                t.Ok(justice.Hearing >= 1f && justice.HearsEnemies, "Justice hears well, the din of the other enemies too");
                t.Ok(justice.PatrolSpeed < 1f && justice.ChaseSpeed < 3.5f, "Justice is slower than a walking player");
                t.Ok(justice.MaxHealth >= pipe.Damage * 10f, "Justice takes more than ten blows of the pipe");
                t.Ok(justice.StaggerResistance >= 0.8f && justice.KnockbackResistance >= 0.8f, "Justice hardly staggers or moves under blows");
                t.Ok(justice.Damage >= 25f && justice.WindupSeconds >= 0.9f, "Justice strikes hard and slowly");
                t.Ok(justice.AttackRange <= justice.SightRange, "Justice strikes whatever it can feel");
                t.Ok(!justice.Shouts, "Justice calls nobody");
            }

            var dokhodyaga = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Dokhodyaga.asset");
            if (dokhodyaga != null) t.Ok(!dokhodyaga.HearsEnemies, "the Dokhodyaga does not go to the din of the other enemies");

            foreach (var (path, mind) in new[] { ("Assets/Prefabs/Enemies/Ram.prefab", typeof(Ram)), ("Assets/Prefabs/Enemies/Justice.prefab", typeof(Justice)) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                t.Ok(prefab != null, $"{path} exists");
                if (prefab == null) continue;
                var ai = prefab.GetComponent(mind) as EnemyAI;
                t.Ok(ai != null && ai.Definition != null, $"{prefab.name} has its mind and definition");
                t.Ok(prefab.layer == LayerMask.NameToLayer("Enemy") && prefab.GetComponent<Health>() != null
                     && prefab.GetComponent<EnemyMotor>() != null && prefab.GetComponent<WrapEntity>() != null, $"{prefab.name} has its parts");
                var body = prefab.GetComponent<Rigidbody2D>();
                t.Ok(body != null && body.mass > 1.2f, $"{prefab.name} is heavier than a Dokhodyaga");
            }

            // A thrown thing is heard; a heavier one farther.
            var light = ScriptableObject.CreateInstance<ItemDefinition>();
            var heavy = ScriptableObject.CreateInstance<ItemDefinition>();
            var hso = new SerializedObject(heavy);
            hso.FindProperty("weight").floatValue = 5f;
            hso.ApplyModifiedPropertiesWithoutUndo();
            t.Ok(Interaction.ThrownItem.NoiseOf(light) >= 4f, "a thrown thing is heard at least four units away");
            t.Ok(Interaction.ThrownItem.NoiseOf(heavy) > Interaction.ThrownItem.NoiseOf(light), "a heavier thing lands louder");
            UnityEngine.Object.DestroyImmediate(light);
            UnityEngine.Object.DestroyImmediate(heavy);
        }

        // ---- wear ----

        private static void WearTests(QuestSelfTest.Checks t)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = "SelfTestWeapon";
            var so = new SerializedObject(weapon);
            so.FindProperty("maxDurability").floatValue = 10f;
            so.FindProperty("durabilityPerHit").floatValue = 1f;
            so.FindProperty("heavyWearMultiplier").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = GameSession.Data.weaponWear.Select(e => new WeaponWearData { item = e.item, durability = e.durability }).ToList();
            try
            {
                GameSession.Data.weaponWear.Clear();
                t.Eq(WeaponWear.Get(weapon), 10f, "a new weapon is whole");
                t.Eq(WeaponWear.Fraction(weapon), 1f, "a new weapon is at 100%");

                t.Ok(!WeaponWear.Wear(weapon, weapon.WearPerHit(false)), "one hit does not break it");
                t.Eq(WeaponWear.Get(weapon), 9f, "one hit takes one point");
                t.Eq(weapon.WearPerHit(true), 3f, "a charged hit costs the multiple");
                t.Ok(!WeaponWear.Wear(weapon, weapon.WearPerHit(true)), "a charged hit does not break it either");
                t.Eq(WeaponWear.Get(weapon), 6f, "a charged hit takes three points");
                t.Eq(WeaponWear.Fraction(weapon), 0.6f, "the share left follows");

                // Kept per kind of weapon, listed once.
                t.Eq(GameSession.Data.weaponWear.Count(e => e.item == weapon.Id), 1, "the wear of one weapon is one entry");

                t.Ok(!WeaponWear.Wear(weapon, 5.9f), "5.9 of 6 left does not break it");
                t.Ok(WeaponWear.Wear(weapon, 1f), "the last point breaks it");
                t.Eq(WeaponWear.Get(weapon), 10f, "a weapon that broke is whole again (a new one)");
                t.Eq(GameSession.Data.weaponWear.Count, 0, "a broken weapon leaves no entry");

                // Losing things on death.
                WeaponWear.Wear(weapon, 4f);
                WeaponWear.ForgetExcept(new[] { weapon.Id });
                t.Eq(WeaponWear.Get(weapon), 6f, "a weapon that is still owned keeps its wear");
                WeaponWear.ForgetExcept(new string[0]);
                t.Eq(WeaponWear.Get(weapon), 10f, "a weapon that was lost forgets its wear");

                var never = ScriptableObject.CreateInstance<WeaponDefinition>();
                var neverSo = new SerializedObject(never);
                neverSo.FindProperty("maxDurability").floatValue = 0f;
                neverSo.ApplyModifiedPropertiesWithoutUndo();
                t.Ok(!never.Wears && !WeaponWear.Wear(never, 100f), "a weapon with no durability never breaks");
                UnityEngine.Object.DestroyImmediate(never);
            }
            finally
            {
                GameSession.Data.weaponWear.Clear();
                GameSession.Data.weaponWear.AddRange(saved);
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }

        // ---- health and damage ----

        private static void HealthTests(QuestSelfTest.Checks t)
        {
            var go = new GameObject("SelfTestHealth");
            try
            {
                var health = go.AddComponent<Health>();
                health.Configure(30f, Team.Enemy);
                t.Ok(health.IsAlive && health.Current == 30f && health.Team == Team.Enemy, "starts alive and full");

                var hurt = new List<float>();
                int deaths = 0;
                health.Damaged += i => hurt.Add(i.Amount);
                health.Died += _ => deaths++;

                IDamageable target = health;
                target.TakeDamage(new DamageInfo { Amount = 10f });
                t.Eq(health.Current, 20f, "damage goes off the health");
                t.Eq(hurt.Count, 1, "a blow that does not kill raises Damaged");

                target.TakeDamage(new DamageInfo { Amount = 0f });
                target.TakeDamage(new DamageInfo { Amount = -5f });
                t.Eq(health.Current, 20f, "no damage, no change (and no healing by a negative blow)");

                health.Invulnerable = true;
                target.TakeDamage(new DamageInfo { Amount = 100f });
                t.Eq(health.Current, 20f, "a dummy takes nothing");
                health.Invulnerable = false;

                target.TakeDamage(new DamageInfo { Amount = 25f });
                t.Ok(!health.IsAlive && health.Current == 0f, "enough damage kills");
                t.Eq(deaths, 1, "Died is raised");
                t.Eq(hurt.Count, 1, "a killing blow raises only Died");

                target.TakeDamage(new DamageInfo { Amount = 25f });
                t.Eq(deaths, 1, "the dead die once");

                health.Revive();
                t.Ok(health.IsAlive && health.Current == health.Max, "revive");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- alert ----

        private static void AlertTests(QuestSelfTest.Checks t)
        {
            var a = new GameObject("SelfTestEnemyA").AddComponent<EnemyAI>();
            var b = new GameObject("SelfTestEnemyB").AddComponent<EnemyAI>();
            var levels = new List<AlertLevel>();
            Action<AlertLevel> listener = levels.Add;
            CombatEvents.AlertChanged += listener;
            try
            {
                AlertDirector.Tick(100f);
                t.Eq(AlertDirector.Current, AlertLevel.Calm, "calm to begin with");

                AlertDirector.Report(a, AlertLevel.Suspicious);
                t.Eq(AlertDirector.Current, AlertLevel.Suspicious, "a rise is at once");
                AlertDirector.Report(b, AlertLevel.Alarm);
                t.Eq(AlertDirector.Current, AlertLevel.Alarm, "the highest of all enemies counts");

                AlertDirector.Report(b, AlertLevel.Calm);
                t.Eq(AlertDirector.Current, AlertLevel.Alarm, "a fall waits: the music does not flicker");
                AlertDirector.Tick(AlertDirector.AlarmHoldSeconds - 0.5f);
                t.Eq(AlertDirector.Current, AlertLevel.Alarm, "still waiting just before the end");
                AlertDirector.Tick(1f);
                t.Eq(AlertDirector.Current, AlertLevel.Suspicious, "it falls to what is left: the other enemy is suspicious");

                AlertDirector.Remove(a);
                AlertDirector.Tick(AlertDirector.SuspicionHoldSeconds + 0.5f);
                t.Eq(AlertDirector.Current, AlertLevel.Calm, "nobody left: calm");

                // A hunter gives up and takes the pause, but is hunted again before it ends: no fall at all.
                AlertDirector.Report(a, AlertLevel.Alarm);
                AlertDirector.Report(a, AlertLevel.Suspicious);
                AlertDirector.Tick(2f);
                AlertDirector.Report(a, AlertLevel.Alarm);
                AlertDirector.Tick(10f);
                t.Eq(AlertDirector.Current, AlertLevel.Alarm, "an alarm that comes back stays");

                t.Ok(levels.SequenceEqual(new[]
                {
                    AlertLevel.Suspicious, AlertLevel.Alarm, AlertLevel.Suspicious, AlertLevel.Calm, AlertLevel.Alarm
                }), "the events came in this order: " + string.Join(",", levels));
            }
            finally
            {
                CombatEvents.AlertChanged -= listener;
                AlertDirector.Remove(a);
                AlertDirector.Remove(b);
                AlertDirector.Tick(100f);
                UnityEngine.Object.DestroyImmediate(a.gameObject);
                UnityEngine.Object.DestroyImmediate(b.gameObject);
            }
        }

        // ---- time ----

        private static void TimeTests(QuestSelfTest.Checks t)
        {
            float oldScale = Time.timeScale;
            try
            {
                ModalState.Reset();
                t.Eq(Time.timeScale, 1f, "normal time to begin with");

                ModalState.Slowdown = 0f;
                t.Eq(Time.timeScale, 0f, "the freeze of a hit stops the time");
                ModalState.Slowdown = 1f;
                t.Eq(Time.timeScale, 1f, "and gives it back");

                // A window pauses, the freeze ending meanwhile must not undo the pause.
                ModalState.Push();
                ModalState.Slowdown = 0.5f;
                t.Eq(Time.timeScale, 0f, "a pause wins over a slowdown");
                ModalState.Slowdown = 1f;
                t.Eq(Time.timeScale, 0f, "the end of a freeze does not lift a pause");
                ModalState.Pop();
                t.Eq(Time.timeScale, 1f, "closing the window gives the time back");

                // The other way round: a freeze is on while a window closes.
                ModalState.Push();
                ModalState.Slowdown = 0f;
                ModalState.Pop();
                t.Eq(Time.timeScale, 0f, "closing a window in the middle of a freeze leaves the freeze");
                ModalState.Slowdown = 1f;
                t.Eq(Time.timeScale, 1f, "which then ends by itself");

                // A window the game goes on behind does not pause.
                ModalState.Push(false);
                t.Eq(Time.timeScale, 1f, "a window that does not pause leaves the time alone");
                ModalState.Pop(false);
            }
            finally
            {
                ModalState.Reset();
                Time.timeScale = oldScale;
            }
        }

        // ---- path search ----

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static void PathTests(QuestSelfTest.Checks t)
        {
            // Open ground.
            var open = Grid(20, 20, wrap: false, blockedCells: new Vector2Int[0], out var openGo);
            try
            {
                var route = new List<Vector2>();
                t.Ok(Find(open, new Vector2(2.5f, 2.5f), new Vector2(15.5f, 12.5f), route), "a path over open ground");
                t.Eq(route.Count, 1, "over open ground the path is one straight line");
                t.Ok(Vector2.Distance(route[route.Count - 1], new Vector2(15.5f, 12.5f)) < 0.01f, "it ends at the target");

                t.Ok(Find(open, new Vector2(3.5f, 3.5f), new Vector2(3.6f, 3.4f), route), "start and target in one cell");
                t.Eq(route.Count, 1, "which is a single point");
            }
            finally { UnityEngine.Object.DestroyImmediate(openGo); }

            // A wall with a gap: x = 10 is blocked except at y = 15.
            var wall = new List<Vector2Int>();
            for (int y = 0; y < 20; y++) if (y != 15) wall.Add(new Vector2Int(10, y));
            var gapped = Grid(20, 20, wrap: false, wall.ToArray(), out var gapGo);
            try
            {
                var route = new List<Vector2>();
                t.Ok(Find(gapped, new Vector2(2.5f, 2.5f), new Vector2(17.5f, 2.5f), route), "a path through the gap in a wall");
                t.Ok(route.Any(p => Mathf.Abs(p.y - 15.5f) < 0.6f), "it goes through the gap");
                t.Ok(!route.Any(p => (int)Mathf.Floor(p.x) == 10 && (int)Mathf.Floor(p.y) != 15), "no corner of it lies inside the wall");
                t.Ok(PathIsFree(gapped, new Vector2(2.5f, 2.5f), route), "and every part of it can be walked");
                t.Ok(PathLength(new Vector2(2.5f, 2.5f), route) < 40f, "and it is not a long way round");
            }
            finally { UnityEngine.Object.DestroyImmediate(gapGo); }

            // The wall closed: nothing leads over.
            var sealedWall = new List<Vector2Int>();
            for (int y = 0; y < 20; y++) sealedWall.Add(new Vector2Int(10, y));
            var closed = Grid(20, 20, wrap: false, sealedWall.ToArray(), out var closedGo);
            try
            {
                var route = new List<Vector2>();
                t.Ok(!Find(closed, new Vector2(2.5f, 2.5f), new Vector2(17.5f, 2.5f), route), "no path to the other side of a closed wall");
                t.Eq(route.Count, 0, "and the route is left empty");
            }
            finally { UnityEngine.Object.DestroyImmediate(closedGo); }

            // No cutting corners: a diagonal wall of single cells must not be slipped through.
            var diagonal = new List<Vector2Int>();
            for (int i = 0; i < 20; i++) diagonal.Add(new Vector2Int(i, 19 - i));
            var slit = Grid(20, 20, wrap: false, diagonal.ToArray(), out var slitGo);
            try
            {
                var route = new List<Vector2>();
                t.Ok(!Find(slit, new Vector2(2.5f, 2.5f), new Vector2(17.5f, 17.5f), route), "a diagonal wall cannot be slipped through between its cells");
            }
            finally { UnityEngine.Object.DestroyImmediate(slitGo); }

            // A target inside a wall is reached at the nearest free cell.
            var pillar = Grid(20, 20, wrap: false, new[] { new Vector2Int(12, 5) }, out var pillarGo);
            try
            {
                var route = new List<Vector2>();
                t.Ok(Find(pillar, new Vector2(2.5f, 5.5f), new Vector2(12.5f, 5.5f), route), "a target inside an obstacle is still approached");
                t.Ok(Vector2.Distance(route[route.Count - 1], new Vector2(12.5f, 5.5f)) < 2.1f, "it ends beside the obstacle");
            }
            finally { UnityEngine.Object.DestroyImmediate(pillarGo); }

            // A level that wraps: the short way is over the edge.
            var world = new GameObject("SelfTestWrap").AddComponent<WrapWorld>();
            var worldSo = new SerializedObject(world);
            worldSo.FindProperty("center").vector2Value = new Vector2(20f, 20f);
            worldSo.FindProperty("size").vector2Value = new Vector2(40f, 40f);
            worldSo.ApplyModifiedPropertiesWithoutUndo();
            typeof(WrapWorld).GetField("<Active>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, world);
            var torus = Grid(40, 40, wrap: true, new Vector2Int[0], out var torusGo);
            try
            {
                var route = new List<Vector2>();
                var from = new Vector2(38.5f, 20.5f);
                var to = new Vector2(1.5f, 20.5f);
                t.Ok(Find(torus, from, to, route), "a path over the seam of a wrapped level");
                t.Ok(PathLength(from, route) < 6f, "it goes over the edge (3 units), not across the level (37): " + PathLength(from, route).ToString("0.0"));
                t.Ok(route.Last().x > 39f, "its points go on beyond the edge instead of jumping back: " + route.Last());

                // Around the corner of the level in both directions at once.
                t.Ok(Find(torus, new Vector2(1.5f, 1.5f), new Vector2(38.5f, 38.5f), route), "a path over the corner seam");
                t.Ok(PathLength(new Vector2(1.5f, 1.5f), route) < 6f, "the corner is crossed too: " + PathLength(new Vector2(1.5f, 1.5f), route).ToString("0.0"));

                // A wall across the middle of the level, with the seam as the only way round.
                var barrier = new List<Vector2Int>();
                for (int y = 0; y < 40; y++) barrier.Add(new Vector2Int(20, y));
                var inner = Grid(40, 40, wrap: true, barrier.ToArray(), out var innerGo);
                try
                {
                    t.Ok(Find(inner, new Vector2(5.5f, 20.5f), new Vector2(35.5f, 20.5f), route), "a level cut by a wall is still passable over its seam");
                    t.Ok(PathLength(new Vector2(5.5f, 20.5f), route) < 12f, "and the way over the seam is the short one: " + PathLength(new Vector2(5.5f, 20.5f), route).ToString("0.0"));
                }
                finally { UnityEngine.Object.DestroyImmediate(innerGo); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(torusGo);
                typeof(WrapWorld).GetField("<Active>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
                UnityEngine.Object.DestroyImmediate(world.gameObject);
            }
        }

        // A grid made in memory: cells of one unit starting at the origin, the given ones blocked.
        private static NavGrid Grid(int width, int height, bool wrap, Vector2Int[] blockedCells, out GameObject owner)
        {
            owner = new GameObject("SelfTestGrid");
            var grid = owner.AddComponent<NavGrid>();
            var type = typeof(NavGrid);
            var blocked = new bool[width * height];
            foreach (var cell in blockedCells) blocked[cell.y * width + cell.x] = true;

            type.GetField("blocked", Private).SetValue(grid, blocked);
            type.GetField("width", Private).SetValue(grid, width);
            type.GetField("height", Private).SetValue(grid, height);
            type.GetField("min", Private).SetValue(grid, Vector2.zero);
            type.GetField("wrap", Private).SetValue(grid, wrap);
            type.GetField("cost", Private).SetValue(grid, new float[width * height]);
            type.GetField("parent", Private).SetValue(grid, new int[width * height]);
            type.GetField("seen", Private).SetValue(grid, new int[width * height]);
            type.GetField("ready", Private).SetValue(grid, true);
            return grid;
        }

        private static bool Find(NavGrid grid, Vector2 from, Vector2 to, List<Vector2> route)
        {
            // The budget of a frame is for the game; the test has no frames.
            typeof(NavGrid).GetField("searchesThisFrame", Private).SetValue(grid, 0);
            return grid.TryFindPath(from, to, route);
        }

        private static float PathLength(Vector2 from, List<Vector2> route)
        {
            float length = 0f;
            var previous = from;
            foreach (var point in route)
            {
                length += Vector2.Distance(previous, point);
                previous = point;
            }
            return length;
        }

        private static bool PathIsFree(NavGrid grid, Vector2 from, List<Vector2> route)
        {
            var method = typeof(NavGrid).GetMethod("LineIsFree", Private);
            var previous = from;
            foreach (var point in route)
            {
                if (!(bool)method.Invoke(grid, new object[] { previous, point })) return false;
                previous = point;
            }
            return true;
        }
    }
}
