using System.Collections.Generic;
using System.Linq;
using ProjectMayham.Audio;
using ProjectMayham.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

namespace ProjectMayham.UI
{
    public class SettingsPanelController : MonoBehaviour
    {
        private const string ResolutionKey = "Settings.ResolutionIndex";

        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider effectsVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeValue;
        [SerializeField] private TMP_Text musicVolumeValue;
        [SerializeField] private TMP_Text effectsVolumeValue;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Button closeButton;

        private readonly List<Resolution> _resolutions = new List<Resolution>();

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (effectsVolumeSlider != null) effectsVolumeSlider.onValueChanged.AddListener(OnEffectsVolumeChanged);
            if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        private void OnEnable()
        {
            LoadVolumeSettings();
            PopulateLanguageOptions();
            PopulateResolutionOptions();
        }

        private void LoadVolumeSettings()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(GameAudio.MasterVolumeKey, 1f));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat(GameAudio.MusicVolumeKey, 1f));
            float effects = Mathf.Clamp01(PlayerPrefs.GetFloat(GameAudio.EffectsVolumeKey, 1f));

            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(master);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(music);
            if (effectsVolumeSlider != null) effectsVolumeSlider.SetValueWithoutNotify(effects);
            SetVolumeText(masterVolumeValue, master);
            SetVolumeText(musicVolumeValue, music);
            SetVolumeText(effectsVolumeValue, effects);

            AudioListener.volume = master;
        }

        private void PopulateLanguageOptions()
        {
            if (languageDropdown == null) return;

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string> { "Русский", "English" });

            var locale = LocalizationSettings.SelectedLocale;
            languageDropdown.SetValueWithoutNotify(locale?.Identifier.Code == GameText.English ? 1 : 0);
        }

        private void PopulateResolutionOptions()
        {
            if (resolutionDropdown == null) return;

            _resolutions.Clear();
            _resolutions.AddRange(Screen.resolutions
                .GroupBy(r => new { r.width, r.height })
                .Select(g => g.Last()));

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(_resolutions.Select(r => $"{r.width} x {r.height}").ToList());

            int currentIndex = _resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
            if (currentIndex < 0) currentIndex = PlayerPrefs.GetInt(ResolutionKey, 0);
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, _resolutions.Count - 1));

            resolutionDropdown.SetValueWithoutNotify(currentIndex);
        }

        public void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(GameAudio.MasterVolumeKey, value);
            SetVolumeText(masterVolumeValue, value);
        }

        public void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(GameAudio.MusicVolumeKey, value);
            SetVolumeText(musicVolumeValue, value);
        }

        public void OnEffectsVolumeChanged(float value)
        {
            GameAudio.SetEffectsVolume(value);
            SetVolumeText(effectsVolumeValue, value);
        }

        private static void SetVolumeText(TMP_Text label, float value)
        {
            if (label != null) label.SetText("{0}%", Mathf.RoundToInt(value * 100f));
        }

        public void OnLanguageChanged(int index)
        {
            if (index < 0 || index > 1) return;

            string code = index == 0 ? GameText.Russian : GameText.English;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return;

            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(GameText.LocalePreference, code);
            PlayerPrefs.Save();
        }

        public void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;

            Resolution resolution = _resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
            PlayerPrefs.SetInt(ResolutionKey, index);
        }

        public void OnCloseClicked()
        {
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}
