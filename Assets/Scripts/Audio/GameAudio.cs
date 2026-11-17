using ProjectMayham.AI;
using ProjectMayham.Combat;
using ProjectMayham.Core;
using ProjectMayham.Player;
using ProjectMayham.World;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ProjectMayham.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        public const string MasterVolumeKey = "Settings.MasterVolume";
        public const string MusicVolumeKey = "Settings.MusicVolume";
        public const string EffectsVolumeKey = "Settings.EffectsVolume";
        private const string EffectsMixerParameter = "EffectsVolume";

        private const float MusicGain = 0.55f;
        private const float AmbienceGain = 0.20f;
        private const float FadeSeconds = 1.25f;
        private const float NearbyDistance = 12f;
        private const float WoundedFraction = 0.30f;
        private const float MonsterAudibleDistance = 18f;
        private const float LowStaminaFraction = 0.35f;
        private const float SprintBreathCost = 0.20f;

        private static GameAudio instance;
        private GameAudioClips clips;
        private AudioSource[] music;
        private AudioSource ambience;
        private AudioSource playerEffects;
        private AudioSource breathing;
        private PlayerStats player;
        private PlayerController controller;
        private int activeMusic;
        private int configuredScene = int.MinValue;
        private bool raid;
        private int raidTheme;
        private float nextRaidCheck;
        private float recentFightUntil;
        private float sprintCost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var library = Resources.Load<GameAudioClips>("GameAudioClips");
            if (library == null)
            {
                Debug.LogError("Missing Resources/GameAudioClips audio configuration.");
                return;
            }

            var root = new GameObject("Game Audio");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameAudio>();
            instance.Initialize(library);
        }

        private void Initialize(GameAudioClips library)
        {
            clips = library;
            music = new[] { CreateSource("Music A", clips.musicBus), CreateSource("Music B", clips.musicBus) };
            ambience = CreateSource("Shelter ambience", clips.musicBus);
            playerEffects = CreateSource("Player hurt", clips.effectsBus);
            breathing = CreateSource("Player breathing", clips.effectsBus);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            SceneManager.activeSceneChanged += OnSceneChanged;
            CombatEvents.HitLanded += OnHitLanded;
        }

        private void Start()
        {
            ApplyEffectsVolume(PlayerPrefs.GetFloat(EffectsVolumeKey, 1f));
            ConfigureScene(SceneManager.GetActiveScene());
        }

        public static void SetEffectsVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(EffectsVolumeKey, volume);
            if (instance != null) instance.ApplyEffectsVolume(volume);
        }

        private void ApplyEffectsVolume(float volume)
        {
            float decibels = 20f * Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f));
            clips.effectsBus.audioMixer.SetFloat(EffectsMixerParameter, decibels);
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            CombatEvents.HitLanded -= OnHitLanded;
            if (instance == this) instance = null;
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup bus, bool persistent = true)
        {
            var child = new GameObject(sourceName);
            if (persistent) child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = bus;
            return source;
        }

        private void OnSceneChanged(Scene previous, Scene next) => ConfigureScene(next);

        private void ConfigureScene(Scene scene)
        {
            if (configuredScene == scene.handle) return;
            configuredScene = scene.handle;
            raid = scene.name == SceneLoader.Raid || scene.name == "CombatTest";
            raidTheme = -1;
            nextRaidCheck = 0f;
            recentFightUntil = 0f;
            sprintCost = 0f;
            player = null;
            controller = null;
            playerEffects.Stop();
            breathing.Stop();
            ambience.Stop();
            if (scene.name == SceneLoader.MainMenu)
                SetMusic(Pick(clips.menuThemes));
            else if (scene.name == SceneLoader.Shelter || scene.name == "ShelterTest")
            {
                SetMusic(clips.shelterMusic);
                PlayLoop(ambience, Pick(clips.shelterAmbience), AmbienceGain * MusicVolume);
            }
            else if (!raid)
                SetMusic(null);
        }

        private static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));

        private void Update()
        {
            if (raid && Time.unscaledTime >= nextRaidCheck)
            {
                nextRaidCheck = Time.unscaledTime + 0.2f;
                UpdateRaidMusic();
            }

            float gain = MusicVolume;
            ambience.volume = AmbienceGain * gain;
            for (int i = 0; i < music.Length; i++)
            {
                var source = music[i];
                float target = i == activeMusic ? MusicGain * gain : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, MusicGain * Time.unscaledDeltaTime / FadeSeconds);
                if (i != activeMusic && source.volume <= 0f) source.Stop();
            }
        }

        private void LateUpdate()
        {
            if (player == null || player.IsDead)
            {
                breathing.Stop();
                sprintCost = 0f;
                return;
            }

            if (sprintCost <= 0f || (controller != null && controller.IsSprinting)) return;
            if (sprintCost >= SprintBreathCost || player.Stamina <= player.StaminaCap * LowStaminaFraction)
                PlayBreath();
            sprintCost = 0f;
        }

        private void UpdateRaidMusic()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerStats>();

            int theme = 0;
            if (player != null && !player.IsDead)
            {
                bool inCombat = AlertDirector.Current == AlertLevel.Alarm || Time.time < recentFightUntil;
                bool wounded = player.HealthFraction <= WoundedFraction;
                if (inCombat && wounded) theme = 3;
                else if (inCombat || wounded) theme = 2;
                else
                {
                    foreach (var enemy in EnemyAI.All)
                    {
                        if (!enemy.IsAlive || Distance(player.transform.position, enemy.transform.position) > NearbyDistance) continue;
                        theme = 1;
                        break;
                    }
                }
            }

            if (theme == raidTheme) return;
            raidTheme = theme;
            SetMusic(clips.raidThemes[theme]);
        }

        private void OnHitLanded(HitEvent hit)
        {
            if (hit.Damage <= 0f) return;
            if (hit.Attacker == null) return;
            bool enemyHitPlayer = hit.Target is PlayerStats && hit.Attacker.TryGetComponent<EnemyAI>(out _);
            bool playerHitEnemy = hit.Target is Health health && health.Team == Team.Enemy &&
                                  hit.Attacker.TryGetComponent<PlayerStats>(out _);
            if (enemyHitPlayer || playerHitEnemy)
                recentFightUntil = Time.time + AlertDirector.AlarmHoldSeconds;
        }

        private void SetMusic(AudioClip clip)
        {
            var current = music[activeMusic];
            if (current.clip == clip && (clip == null || current.isPlaying)) return;

            activeMusic = 1 - activeMusic;
            var incoming = music[activeMusic];
            if (incoming.clip == clip && incoming.isPlaying) return;
            // All four raid variations share the same arrangement and length.
            float startTime = raid && current.isPlaying && System.Array.IndexOf(clips.raidThemes, current.clip) >= 0
                ? current.time
                : 0f;
            PlayLoop(incoming, clip, 0f, startTime);
        }

        private static void PlayLoop(AudioSource source, AudioClip clip, float volume, float startTime = 0f)
        {
            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            if (clip == null) return;
            source.time = startTime % clip.length;
            source.Play();
        }

        private static AudioClip Pick(AudioClip[] choices) => choices[Random.Range(0, choices.Length)];

        public static void PlayerHurt()
        {
            if (instance != null) instance.playerEffects.PlayOneShot(instance.clips.playerHurt, 0.8f);
        }

        public static void StaminaSpent(PlayerStats stats, PlayerController movement, float amount, bool sprinting)
        {
            if (instance == null || amount <= 0f) return;
            instance.player = stats;
            instance.controller = movement;
            if (sprinting) instance.sprintCost += amount / stats.StaminaCap;
            else if (stats.Stamina <= stats.StaminaCap * LowStaminaFraction) instance.PlayBreath();
        }

        private void PlayBreath()
        {
            if (!breathing.isPlaying) breathing.PlayOneShot(clips.heavyBreathing, 0.65f);
        }

        public static void MonsterHurt(Vector2 position)
        {
            if (instance != null) instance.PlayMonster(position, instance.clips.monsterHurt, 0.85f);
        }

        public static void MonsterVoice(Vector2 position)
        {
            if (instance != null) instance.PlayMonster(position, instance.clips.monsterVoices, 0.7f);
        }

        private void PlayMonster(Vector2 position, AudioClip[] choices, float gain)
        {
            if (player == null) player = FindFirstObjectByType<PlayerStats>();
            if (player == null) return;

            Vector2 delta = Offset(player.transform.position, position);
            float distance = delta.magnitude;
            if (distance >= MonsterAudibleDistance) return;

            var clip = Pick(choices);
            var source = CreateSource("Monster voice", clips.effectsBus, persistent: false);
            source.panStereo = Mathf.Clamp(delta.x / 8f, -0.85f, 0.85f);
            source.volume = gain * (1f - distance / MonsterAudibleDistance);
            source.clip = clip;
            source.Play();
            Destroy(source.gameObject, clip.length + 0.1f);
        }

        private static float Distance(Vector2 from, Vector2 to) => Offset(from, to).magnitude;

        private static Vector2 Offset(Vector2 from, Vector2 to)
        {
            var wrap = WrapWorld.Active;
            return wrap != null ? wrap.Delta(from, to) : to - from;
        }
    }
}
