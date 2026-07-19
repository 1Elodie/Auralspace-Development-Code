using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRMaterialSystem
{
    /// <summary>
    /// 管理室外声源与玩家控制面板。
    /// 面板只控制声音类型、播放状态和整体音量，不负责创建或查找场景对象，所有必需引用均由 Inspector 明确绑定。
    /// </summary>
    public class TrafficNoiseController : MonoBehaviour
    {
        [Header("Traffic Sources")]
        [Tooltip("位于教室两侧窗外的交通声源。开启和关闭时会同时控制全部交通声源。")]
        [SerializeField] private AudioSource[] trafficSources;

        [Header("Rain Sources")]
        [Tooltip("位于教室窗外的下雨声源。与交通声互斥播放，避免两个外界环境声同时干扰判断。")]
        [SerializeField] private AudioSource[] rainSources;

        [Header("Panel")]
        [Tooltip("折叠状态下显示的入口按钮容器。")]
        [SerializeField] private GameObject launcherRoot;

        [Tooltip("展开后显示外界声音选项和音量滑条的控制面板。")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private Button openPanelButton;
        [SerializeField] private Button closePanelButton;
        [SerializeField] private Toggle trafficToggle;
        [SerializeField] private Toggle rainToggle;
        [SerializeField] private Slider outdoorVolumeSlider;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text volumeLabel;

        [Header("Initial State")]
        [Tooltip("是否在进入场景时立即播放交通声。默认关闭，避免未经玩家选择就播放。")]
        [SerializeField] private bool playOnStart;

        [Tooltip("外界声源的初始线性音量，滑条运行时会同步修改这个值。")]
        [Range(0f, 1f)]
        [SerializeField] private float initialOutdoorVolume = 0.45f;

        private bool _suppressToggleCallbacks;
        private float _outdoorVolume;

        /// <summary>当前是否正在播放室外交通声。</summary>
        public bool IsTrafficNoiseEnabled { get; private set; }

        /// <summary>当前是否正在播放室外下雨声。</summary>
        public bool IsRainSoundEnabled { get; private set; }

        private void Awake()
        {
            if (!ValidateRequiredReferences())
                return;

            openPanelButton.onClick.AddListener(OpenPanel);
            closePanelButton.onClick.AddListener(ClosePanel);
            trafficToggle.onValueChanged.AddListener(SetTrafficNoiseEnabled);
            rainToggle.onValueChanged.AddListener(SetRainSoundEnabled);
            outdoorVolumeSlider.onValueChanged.AddListener(SetOutdoorVolume);

            launcherRoot.SetActive(true);
            panelRoot.SetActive(false);

            _outdoorVolume = Mathf.Clamp01(initialOutdoorVolume);
            outdoorVolumeSlider.minValue = 0f;
            outdoorVolumeSlider.maxValue = 1f;
            outdoorVolumeSlider.SetValueWithoutNotify(_outdoorVolume);
            ApplyOutdoorVolume(_outdoorVolume);
            UpdateVolumeLabel();

            trafficToggle.SetIsOnWithoutNotify(playOnStart);
            rainToggle.SetIsOnWithoutNotify(false);
            SetTrafficNoiseEnabled(playOnStart);
        }

        private void OnDestroy()
        {
            if (openPanelButton != null) openPanelButton.onClick.RemoveListener(OpenPanel);
            if (closePanelButton != null) closePanelButton.onClick.RemoveListener(ClosePanel);
            if (trafficToggle != null) trafficToggle.onValueChanged.RemoveListener(SetTrafficNoiseEnabled);
            if (rainToggle != null) rainToggle.onValueChanged.RemoveListener(SetRainSoundEnabled);
            if (outdoorVolumeSlider != null) outdoorVolumeSlider.onValueChanged.RemoveListener(SetOutdoorVolume);
        }

        /// <summary>展开交通声控制面板，声音状态保持不变。</summary>
        public void OpenPanel()
        {
            if (launcherRoot != null) launcherRoot.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>折叠控制面板，声音状态保持不变。</summary>
        public void ClosePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (launcherRoot != null) launcherRoot.SetActive(true);
        }

        /// <summary>
        /// 设置交通声播放状态。每次开启都先停止并归零，确保循环始终从音频开头重新播放。
        /// 开启交通声时会关闭下雨声，让外界声源保持二选一状态。
        /// </summary>
        public void SetTrafficNoiseEnabled(bool enabled)
        {
            if (_suppressToggleCallbacks) return;

            IsTrafficNoiseEnabled = enabled;

            StopSources(trafficSources);
            if (enabled)
            {
                // 交通声和下雨声互斥：玩家选择交通声时，立即停止雨声并同步 UI，避免两种室外声叠加。
                IsRainSoundEnabled = false;
                StopSources(rainSources);
                SetToggleWithoutNotify(rainToggle, false);
                PlaySourcesFromStart(trafficSources);
            }

            UpdateStatusLabel();
        }

        /// <summary>
        /// 设置下雨声播放状态。每次开启都从音频开头播放，并关闭交通声。
        /// </summary>
        public void SetRainSoundEnabled(bool enabled)
        {
            if (_suppressToggleCallbacks) return;

            IsRainSoundEnabled = enabled;

            StopSources(rainSources);
            if (enabled)
            {
                // 下雨声与交通声使用同一套外界声源面板，开启雨声时要显式停掉交通声。
                IsTrafficNoiseEnabled = false;
                StopSources(trafficSources);
                SetToggleWithoutNotify(trafficToggle, false);
                PlaySourcesFromStart(rainSources);
            }

            UpdateStatusLabel();
        }

        /// <summary>
        /// 调整所有外界声源的 AudioSource 音量。玻璃隔声仍由 Audio Mixer 控制，这里只负责用户可调的整体响度。
        /// </summary>
        public void SetOutdoorVolume(float volume)
        {
            _outdoorVolume = Mathf.Clamp01(volume);

            // 允许 Slider 回调和代码直接调用共用同一个入口；直接调用时也同步 UI，避免显示值和实际音量分离。
            if (outdoorVolumeSlider != null && !Mathf.Approximately(outdoorVolumeSlider.value, _outdoorVolume))
                outdoorVolumeSlider.SetValueWithoutNotify(_outdoorVolume);

            ApplyOutdoorVolume(_outdoorVolume);
            UpdateVolumeLabel();
        }

        private void PlaySourcesFromStart(AudioSource[] sources)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null) continue;

                // 每次重新打开都回到音频开头，符合“打开即从头播放”的交互预期。
                source.Stop();
                source.time = 0f;
                source.Play();
            }
        }

        private void StopSources(AudioSource[] sources)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null) continue;

                source.Stop();
                source.time = 0f;
            }
        }

        private void ApplyOutdoorVolume(float volume)
        {
            ApplyVolumeToSources(trafficSources, volume);
            ApplyVolumeToSources(rainSources, volume);
        }

        private void ApplyVolumeToSources(AudioSource[] sources, float volume)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null) continue;

                source.volume = volume;
            }
        }

        private void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null) return;

            _suppressToggleCallbacks = true;
            toggle.SetIsOnWithoutNotify(value);
            _suppressToggleCallbacks = false;
        }

        private void UpdateStatusLabel()
        {
            if (statusLabel == null) return;

            if (IsTrafficNoiseEnabled)
                statusLabel.text = "Traffic noise";
            else if (IsRainSoundEnabled)
                statusLabel.text = "Rain sound";
            else
                statusLabel.text = "Off";
        }

        private void UpdateVolumeLabel()
        {
            if (volumeLabel == null) return;

            int percent = Mathf.RoundToInt(_outdoorVolume * 100f);
            volumeLabel.text = "Volume: " + percent + "%";
        }

        /// <summary>只在真实 Inspector 边界检查必需引用，缺失时明确暴露配置问题。</summary>
        private bool ValidateRequiredReferences()
        {
            bool valid = trafficSources != null && trafficSources.Length > 0 &&
                         rainSources != null && rainSources.Length > 0 &&
                         launcherRoot != null && panelRoot != null &&
                         openPanelButton != null && closePanelButton != null &&
                         trafficToggle != null && rainToggle != null && outdoorVolumeSlider != null;

            if (!valid)
                Debug.LogError("[TrafficNoiseController] 外界声源或控制面板引用未完整配置。", this);

            return valid;
        }
    }
}
