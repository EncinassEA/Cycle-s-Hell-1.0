using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyclesHell.UI
{
    /// <summary>
    /// Aplica el Brillo guardado en GameSettingsManager al Post Exposure del Global Volume de URP
    /// usando la instancia de runtime (volume.profile) para no modificar el asset en disco.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class BrightnessSettingsApplier : MonoBehaviour
    {
        private Volume volume;
        private ColorAdjustments colorAdjustments;
        private float baselinePostExposure;
        private bool initialized;

        private void Awake()
        {
            volume = GetComponent<Volume>();
        }

        private void OnEnable()
        {
            GameSettingsManager.OnSettingsChanged += ApplyBrightness;
            if (initialized)
            {
                ApplyBrightness();
            }
        }

        private void OnDisable()
        {
            GameSettingsManager.OnSettingsChanged -= ApplyBrightness;
        }

        private void Start()
        {
            EnsureProfileSetup();
            ApplyBrightness();
        }

        private void EnsureProfileSetup()
        {
            if (initialized || volume == null || volume.profile == null)
            {
                return;
            }

            if (!volume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = volume.profile.Add<ColorAdjustments>(true);
                baselinePostExposure = 0f;
            }
            else
            {
                baselinePostExposure = colorAdjustments.postExposure.overrideState
                    ? colorAdjustments.postExposure.value
                    : 0f;
            }

            colorAdjustments.postExposure.overrideState = true;
            initialized = true;
        }

        public void ApplyBrightness()
        {
            if (!initialized)
            {
                EnsureProfileSetup();
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = baselinePostExposure + GameSettingsManager.Brightness;
            }
        }
    }
}
