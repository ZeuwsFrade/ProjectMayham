using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMayham.UI
{
    public class SettingsPanelController : MonoBehaviour
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string MusicVolumeKey = "Settings.MusicVolume";
        private const string LanguageKey = "Settings.LanguageIndex";
        private const string ResolutionKey = "Settings.ResolutionIndex";

        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Button closeButton;

        private readonly List<Resolution> _resolutions = new List<Resolution>();

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
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
            float master = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            float music = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);

            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(master);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(music);

            AudioListener.volume = master;
        }

        private void PopulateLanguageOptions()
        {
            if (languageDropdown == null) return;

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string> { "Русский", "English" });

            int savedIndex = PlayerPrefs.GetInt(LanguageKey, 0);
            languageDropdown.SetValueWithoutNotify(Mathf.Clamp(savedIndex, 0, languageDropdown.options.Count - 1));
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
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        // Пока в проекте нет системы музыки — значение только сохраняется на будущее.
        public void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, value);
        }

        // Заглушка: пакет локализации не подключён, реального перевода пока нет.
        public void OnLanguageChanged(int index)
        {
            PlayerPrefs.SetInt(LanguageKey, index);
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
            gameObject.SetActive(false);
        }
    }
}
