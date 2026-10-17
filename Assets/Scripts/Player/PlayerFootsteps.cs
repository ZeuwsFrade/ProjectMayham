using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMayham.Player
{
    /// <summary>
    /// The sound of the player's steps: one step for every stride walked, so the steps follow the real speed (quicker
    /// when sprinting, slower with a heavy load or a raised weapon) and stop when the player stands, climbs or is dead.
    /// The sound is separate recordings of one step each, or one track of several steps: then every step of the
    /// player plays one step of the track, from where it begins (found in the Editor when the track is set) until the
    /// next one, fading out. Every step is a little higher or lower and louder or quieter than the last, and never the
    /// same recording twice in a row. Or a whole track of walking played round and round while the player walks: it
    /// comes in and fades out softly, starts from its beginning again after every stop, and plays a little faster or
    /// slower with the speed. Only the sound: the noise enemies hear is <see cref="Combat.PlayerNoise"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        public enum SoundKind
        {
            /// <summary>Separate recordings of one step each.</summary>
            Steps = 0,
            /// <summary>One recording of several steps, played a step at a time.</summary>
            Track = 1,
            /// <summary>A whole recording of walking, played round and round while the player walks.</summary>
            Loop = 2,
        }

        // Steps of a track can overlap when sprinting: a few sources play them side by side.
        private const int VoiceCount = 3;
        // A step of a track fades out over its last moments instead of stopping with a click.
        private const float FadeOut = 0.08f;

        [SerializeField] private SoundKind kind = SoundKind.Steps;
        [Tooltip("Separate recordings of one step each; a random one is played for every step.")]
        [SerializeField] private AudioClip[] steps;

        [Tooltip("One recording of several steps.")]
        [SerializeField] private AudioClip track;
        [Tooltip("Where the steps of the track begin, in seconds. Found in the Editor.")]
        [SerializeField] private float[] trackSteps = new float[0];
        [Tooltip("The volume of every step of the track (evens out loud and quiet ones). Found with the steps.")]
        [SerializeField] private float[] trackGains = new float[0];
        // How the Editor finds the steps of the track: kept with the track, read only by the Editor.
#pragma warning disable CS0414
        [Tooltip("Quieter than this share of the loudest step is not a step (for finding the steps).")]
        [SerializeField, Range(0.05f, 0.6f)] private float trackThreshold = 0.18f;
        [Tooltip("Loud steps of the track are made as quiet as the usual ones (for finding the steps).")]
        [SerializeField] private bool trackEvenVolume = true;
#pragma warning restore CS0414
        [Tooltip("A step of the track ends here at the latest, in seconds (or where the next one begins).")]
        [SerializeField, Range(0.15f, 1.5f)] private float trackStepLength = 0.45f;

        [Tooltip("A recording of walking, played round and round while the player walks, from its start after every stop.")]
        [SerializeField] private AudioClip loopTrack;
        [Tooltip("Optional: a recording of running, for a sprint. Empty: the walking one, a little faster.")]
        [SerializeField] private AudioClip loopSprintTrack;
        [Tooltip("Seconds the loop takes to come in when the player sets off and to fade out when they stop.")]
        [SerializeField, Range(0.02f, 1f)] private float loopFade = 0.15f;
        [Tooltip("The loop plays faster or slower with the player's speed, within this range (1: as recorded).")]
        [SerializeField] private Vector2 loopRate = new Vector2(0.85f, 1.15f);

        [Header("Rhythm")]
        [Tooltip("Distance walked between two steps, in world units (a tile is one).")]
        [SerializeField, Min(0.2f)] private float stride = 1.6f;
        [Tooltip("Slower than this is standing still: no steps.")]
        [SerializeField, Min(0f)] private float minSpeed = 0.8f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float sprintVolume = 0.8f;
        [Tooltip("The pitch of every step is picked in this range.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);
        [Tooltip("The volume of every step is multiplied by a value picked in this range.")]
        [SerializeField] private Vector2 volumeJitter = new Vector2(0.85f, 1f);
        [Tooltip("Optional: the mixer group the steps play through.")]
        [SerializeField] private AudioMixerGroup output;

        private class Voice
        {
            public AudioSource Source;
            public bool Segment;      // plays a step of the track and has to be stopped at End
            public float End;
            public float Volume;
            public float StartedAt;
            public bool Loop;         // plays the whole track round and round, its volume going toward Target
            public float Target;
        }

        private readonly List<Voice> voices = new List<Voice>();
        private PlayerController controller;
        private PlayerStats stats;
        private float walked;
        private int lastStep = -1;
        private Voice loopVoice;   // the loop of the walk going on now; null while the player stands

        public SoundKind Kind => kind;
        /// <summary>True while the loop of a walk plays (it is not fading out after a stop).</summary>
        public bool LoopPlaying => loopVoice != null;
        /// <summary>Steps played since the start, for tests.</summary>
        public int StepCount { get; private set; }

        /// <summary>
        /// The part of a track that is step <paramref name="index"/>: from where it begins to where the next one
        /// begins, at most <paramref name="maxLength"/> seconds and not past the end of the track.
        /// </summary>
        public static (float start, float end) TrackSegment(IReadOnlyList<float> starts, int index, float trackLength, float maxLength)
        {
            float start = Mathf.Clamp(starts[index], 0f, trackLength);
            float end = Mathf.Min(trackLength, start + maxLength);
            if (index + 1 < starts.Count) end = Mathf.Min(end, starts[index + 1]);
            return (start, Mathf.Max(start, end));
        }

        /// <summary>
        /// How fast the loop plays: the player's speed against the speed the recording was made at, kept within the
        /// range so the steps do not sound too high or too low.
        /// </summary>
        public static float LoopRate(float speed, float recordedSpeed, Vector2 range)
        {
            if (recordedSpeed <= 0f) return 1f;
            return Mathf.Clamp(speed / recordedSpeed, Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;   // top-down: the player is always in the middle of the screen
                source.outputAudioMixerGroup = output;
                voices.Add(new Voice { Source = source });
            }
            walked = stride * 0.5f;
        }

        private void Update()
        {
            FadeSegments();

            float speed = controller.Velocity.magnitude;
            // A window that pauses the game is a stop too: the body keeps its velocity, but nobody walks.
            bool walking = Time.timeScale > 0f && speed >= minSpeed && !controller.MovementLocked && (stats == null || !stats.IsDead);
            UpdateLoop(kind == SoundKind.Loop && walking, speed);
            FadeLoops();
            if (kind == SoundKind.Loop) return;

            if (!walking)
            {
                // After a stop the first step comes at half a stride, not at once and not a whole stride later.
                walked = stride * 0.5f;
                return;
            }

            walked += speed * Time.deltaTime;
            if (walked < stride) return;
            // One step per frame at most: a long frame does not make a burst of steps.
            walked = Mathf.Min(walked - stride, stride * 0.5f);
            PlayStep();
        }

        // Steps of the track end where the next one begins: the volume goes down over the last moments, then stop.
        // Runs while the game is paused too, so a step never plays on into the next one.
        private void FadeSegments()
        {
            foreach (var voice in voices)
            {
                if (!voice.Segment) continue;
                float left = voice.End - voice.Source.time;
                if (!voice.Source.isPlaying || left <= 0f)
                {
                    voice.Source.Stop();
                    voice.Segment = false;
                }
                else if (left < FadeOut) voice.Source.volume = voice.Volume * left / FadeOut;
            }
        }

        // The loop of the walk: started from the beginning when the player sets off, faded out when they stop (or
        // change between walking and running tracks); the next walk starts it from the beginning again.
        private void UpdateLoop(bool walking, float speed)
        {
            bool running = walking && controller.IsSprinting && loopSprintTrack != null;
            var wanted = !walking ? null : running ? loopSprintTrack : loopTrack;
            if (loopVoice != null && (wanted == null || loopVoice.Source.clip != wanted))
            {
                loopVoice.Target = 0f;
                loopVoice = null;
            }
            if (wanted == null) return;

            if (loopVoice == null)
            {
                loopVoice = FreeVoice();
                var source = loopVoice.Source;
                source.Stop();
                source.clip = wanted;
                source.loop = true;
                source.volume = 0f;
                loopVoice.Segment = false;
                loopVoice.Loop = true;
                loopVoice.StartedAt = Time.unscaledTime;
                source.Play();
                source.timeSamples = 0;
            }
            loopVoice.Source.pitch = LoopRate(speed, running ? controller.SprintSpeed : controller.WalkSpeed, loopRate);
            loopVoice.Target = controller.IsSprinting ? sprintVolume : walkVolume;
        }

        // Loops come in and fade out; a faded one stops. Unscaled time: a stop for a window fades out too.
        private void FadeLoops()
        {
            float rate = Mathf.Max(walkVolume, sprintVolume, 0.01f) / loopFade;
            foreach (var voice in voices)
            {
                if (!voice.Loop) continue;
                voice.Source.volume = Mathf.MoveTowards(voice.Source.volume, voice.Target, rate * Time.unscaledDeltaTime);
                if (voice == loopVoice || voice.Source.volume > 0f) continue;
                voice.Source.Stop();
                voice.Source.loop = false;
                voice.Loop = false;
            }
        }

        private void PlayStep()
        {
            float pitch = Random.Range(pitchRange.x, pitchRange.y);
            float volume = (controller.IsSprinting ? sprintVolume : walkVolume) * Random.Range(volumeJitter.x, volumeJitter.y);
            if (kind == SoundKind.Track) PlayTrackStep(pitch, volume);
            else PlayRecording(pitch, volume);
        }

        private void PlayRecording(float pitch, float volume)
        {
            if (steps == null || steps.Length == 0) return;
            var clip = steps[Pick(steps.Length)];
            if (clip == null) return;

            var voice = FreeVoice();
            voice.Source.pitch = pitch;
            voice.Source.volume = 1f;
            voice.StartedAt = Time.unscaledTime;
            voice.Source.PlayOneShot(clip, volume);
            StepCount++;
        }

        private void PlayTrackStep(float pitch, float volume)
        {
            if (track == null) return;

            // Without the steps found (the Editor was not asked), the start of the track is one step.
            float start = 0f, end = Mathf.Min(track.length, trackStepLength), gain = 1f;
            int count = trackSteps != null ? trackSteps.Length : 0;
            if (count > 0)
            {
                int index = Pick(count);
                (start, end) = TrackSegment(trackSteps, index, track.length, trackStepLength);
                if (trackGains != null && index < trackGains.Length) gain = trackGains[index];
            }

            var voice = FreeVoice();
            var source = voice.Source;
            source.Stop();
            source.loop = false;
            voice.Loop = false;
            source.clip = track;
            source.pitch = pitch;
            voice.Volume = volume * gain;
            source.volume = voice.Volume;
            voice.End = end;
            voice.Segment = true;
            voice.StartedAt = Time.unscaledTime;
            source.Play();
            source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(start * track.frequency), 0, Mathf.Max(0, track.samples - 1));
            StepCount++;
        }

        // A source that is quiet, else the one that has played longest.
        private Voice FreeVoice()
        {
            Voice oldest = voices[0];
            foreach (var voice in voices)
            {
                if (!voice.Source.isPlaying) return voice;
                if (voice.StartedAt < oldest.StartedAt) oldest = voice;
            }
            return oldest;
        }

        // A random one of count, never the same twice in a row.
        private int Pick(int count)
        {
            int index = 0;
            if (count > 1)
            {
                if (lastStep < 0 || lastStep >= count) index = Random.Range(0, count);
                else
                {
                    index = Random.Range(0, count - 1);
                    if (index >= lastStep) index++;
                }
            }
            lastStep = index;
            return index;
        }
    }
}
