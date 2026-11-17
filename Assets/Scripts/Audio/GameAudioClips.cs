using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMayham.Audio
{
    public sealed class GameAudioClips : ScriptableObject
    {
        public AudioMixerGroup musicBus;
        public AudioMixerGroup effectsBus;
        public AudioClip[] menuThemes;
        public AudioClip secretMenuTheme;
        public AudioClip shelterMusic;
        public AudioClip[] shelterAmbience;
        public AudioClip[] raidThemes;
        public AudioClip playerHurt;
        public AudioClip heavyBreathing;
        public AudioClip[] monsterHurt;
        public AudioClip[] monsterVoices;
    }
}
