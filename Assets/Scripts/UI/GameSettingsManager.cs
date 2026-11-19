using System;
using UnityEngine;

namespace CyclesHell.UI
{
    /// <summary>
    /// Gestor persistente de configuración de usuario mediante PlayerPrefs:
    /// - Volumen General (0..1 aplicado a AudioListener.volume)
    /// - Brillo (-1.0..+1.5 EV aplicado al Post Exposure del URP Global Volume)
    /// - Sensibilidad del ratón (0.04..0.40 aplicada a FirstPersonController)
    /// </summary>
    public static class GameSettingsManager
    {
        private const string VolumeKey = "CyclesHell_MasterVolume";
        private const string BrightnessKey = "CyclesHell_Brightness";
        private const string SensitivityKey = "CyclesHell_MouseSensitivity";

        public const float DefaultVolume = 0.85f;
        public const float DefaultBrightness = 0.0f;
        public const float DefaultSensitivity = 0.14f;

        public const float MinBrightness = -1.0f;
        public const float MaxBrightness = 1.5f;
        public const float MinSensitivity = 0.04f;
        public const float MaxSensitivity = 0.40f;

        private static bool initialized;
        private static float masterVolume = DefaultVolume;
        private static float brightness = DefaultBrightness;
        private static float mouseSensitivity = DefaultSensitivity;

        public static event Action OnSettingsChanged;

        public static float MasterVolume
        {
            get { EnsureInitialized(); return masterVolume; }
        }

        public static float Brightness
        {
            get { EnsureInitialized(); return brightness; }
        }

        public static float MouseSensitivity
        {
            get { EnsureInitialized(); return mouseSensitivity; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
            ApplySavedSettings();
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), MinBrightness, MaxBrightness);
            mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        }

        public static void ApplySavedSettings()
        {
            EnsureInitialized();
            AudioListener.volume = masterVolume;
            OnSettingsChanged?.Invoke();
        }

        public static void SetMasterVolume(float value)
        {
            EnsureInitialized();
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(VolumeKey, masterVolume);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetBrightness(float value)
        {
            EnsureInitialized();
            brightness = Mathf.Clamp(value, MinBrightness, MaxBrightness);
            PlayerPrefs.SetFloat(BrightnessKey, brightness);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void SetMouseSensitivity(float value)
        {
            EnsureInitialized();
            mouseSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, mouseSensitivity);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static void ResetToDefaults()
        {
            EnsureInitialized();
            masterVolume = DefaultVolume;
            brightness = DefaultBrightness;
            mouseSensitivity = DefaultSensitivity;

            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(VolumeKey, masterVolume);
            PlayerPrefs.SetFloat(BrightnessKey, brightness);
            PlayerPrefs.SetFloat(SensitivityKey, mouseSensitivity);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke();
        }
    }
}
