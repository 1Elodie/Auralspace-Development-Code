using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRMenuSystem
{
    /// <summary>
    /// 管理教师桌附近的人声播放、开关和独立音量，不改变室外声音系统的状态。
    /// </summary>
    public sealed class InsideSoundController : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField, Tooltip("位于前方电脑桌附近的 3D 人声音源。")]
        private AudioSource speechSource;

        [Header("Controls")]
        // 控件由 Inside Sound 面板显式绑定，避免运行时扫描到 Outside Sound 的同类控件。
        [SerializeField] private Toggle speechToggle;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text volumeLabel;

        [Header("Initial State")]
        [SerializeField, Tooltip("进入场景时是否立即播放人声；默认关闭，由玩家主动开启。")]
        private bool playOnStart;

        [SerializeField, Range(0f, 1f), Tooltip("室内人声的初始线性音量。")]
        private float initialVolume = 0.65f;

        public bool IsEnabled { get; private set; }

        private void Awake()
        {
            if (!ValidateRequiredReferences())
            {
                return;
            }

            speechToggle.onValueChanged.AddListener(SetEnabled);
            volumeSlider.onValueChanged.AddListener(SetVolume);

            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(initialVolume));
            SetVolume(initialVolume);

            speechToggle.SetIsOnWithoutNotify(playOnStart);
            SetEnabled(playOnStart);
        }

        private void OnDestroy()
        {
            if (speechToggle != null) speechToggle.onValueChanged.RemoveListener(SetEnabled);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
        }

        /// <summary>开启时从音频开头循环播放，关闭时停止并归零，保证每次试听行为一致。</summary>
        public void SetEnabled(bool enabled)
        {
            if (speechSource == null)
            {
                return;
            }

            IsEnabled = enabled;
            speechSource.Stop();
            speechSource.time = 0f;
            if (enabled)
            {
                speechSource.Play();
            }

            if (speechToggle != null && speechToggle.isOn != enabled)
            {
                speechToggle.SetIsOnWithoutNotify(enabled);
            }

            if (statusLabel != null)
            {
                statusLabel.text = enabled ? "Playing" : "Off";
            }
        }

        /// <summary>只调整该人声音源的音量，不影响交通声、雨声或全局 Audio Listener。</summary>
        public void SetVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            if (speechSource != null)
            {
                speechSource.volume = clampedVolume;
            }

            if (volumeSlider != null && !Mathf.Approximately(volumeSlider.value, clampedVolume))
            {
                volumeSlider.SetValueWithoutNotify(clampedVolume);
            }

            if (volumeLabel != null)
            {
                volumeLabel.text = "Volume: " + Mathf.RoundToInt(clampedVolume * 100f) + "%";
            }
        }

        private bool ValidateRequiredReferences()
        {
            bool valid = speechSource != null && speechSource.clip != null && speechToggle != null &&
                         volumeSlider != null && statusLabel != null && volumeLabel != null;
            if (!valid)
            {
                Debug.LogError("[InsideSoundController] 人声音源或 Inside Sound UI 引用未完整配置。", this);
            }

            return valid;
        }
    }
}
