using UnityEngine;
using UnityEngine.Audio;

namespace VRMaterialSystem
{
    /// <summary>
    /// 将围护结构材料的简化隔声数据应用到外界声音 Audio Mixer。
    /// 保留类名是为了不破坏场景里已经绑定好的 GlazingInsulationController 引用。
    /// </summary>
    public class GlazingInsulationController : MonoBehaviour
    {
        private const string TrafficVolumeParameter = "TrafficVolume";
        private const string TrafficLowPassParameter = "TrafficLowPass";
        private const float MinimumLinearVolume = 0.0001f;

        [Tooltip("包含 TrafficNoise 组以及两个暴露参数的 Audio Mixer。")]
        [SerializeField] private AudioMixer trafficMixer;

        [Tooltip("进入场景时使用的玻璃隔声预设。")]
        [SerializeField] private GlazingAcousticData initialGlazing;

        [Header("Initial Surface Insulation")]
        [Tooltip("进入场景时使用的墙体隔声预设，应与当前默认墙面材质保持一致。")]
        [SerializeField] private SurfaceInsulationData initialWall;

        [Tooltip("进入场景时使用的天花板隔声预设，应与当前默认天花板材质保持一致。")]
        [SerializeField] private SurfaceInsulationData initialCeiling;

        [Tooltip("进入场景时使用的地面隔声预设，应与当前默认地面材质保持一致。")]
        [SerializeField] private SurfaceInsulationData initialFloor;

        [Header("Surface Contribution Weights")]
        [Range(0f, 1f)]
        [Tooltip("玻璃通常是外界噪声进入室内的主要路径，因此默认权重最高；后续可以按真实窗墙比调整。")]
        [SerializeField] private float glazingContributionWeight = 0.55f;

        [Range(0f, 1f)]
        [Tooltip("墙体对外界声源的贡献权重。当前为简化模型参数，不代表真实面积。")]
        [SerializeField] private float wallContributionWeight = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("天花板对外界声源的贡献权重。当前为简化模型参数，不代表真实面积。")]
        [SerializeField] private float ceilingContributionWeight = 0.1f;

        [Range(0f, 1f)]
        [Tooltip("地面对外界声源的贡献权重。当前主要用于表现地毯/木地板对室内感知响度的差异。")]
        [SerializeField] private float floorContributionWeight = 0.1f;

        /// <summary>当前已经应用到 Audio Mixer 的玻璃预设。</summary>
        public GlazingAcousticData CurrentGlazing { get; private set; }

        /// <summary>当前参与外界声音计算的墙体隔声预设。</summary>
        public SurfaceInsulationData CurrentWall { get; private set; }

        /// <summary>当前参与外界声音计算的天花板隔声预设。</summary>
        public SurfaceInsulationData CurrentCeiling { get; private set; }

        /// <summary>当前参与外界声音计算的地面隔声预设。</summary>
        public SurfaceInsulationData CurrentFloor { get; private set; }

        private void Start()
        {
            if (initialWall != null)
                SetCurrentSurface(initialWall, false);

            if (initialCeiling != null)
                SetCurrentSurface(initialCeiling, false);

            if (initialFloor != null)
                SetCurrentSurface(initialFloor, false);

            if (initialGlazing != null)
                CurrentGlazing = initialGlazing;

            ApplyCombinedInsulation();
        }

        /// <summary>
        /// 应用选中的玻璃预设。线性透射音量会和其他表面预设组合后，再转换为 Mixer dB。
        /// </summary>
        public bool ApplyGlazing(GlazingAcousticData selectedGlazing)
        {
            if (selectedGlazing == null)
            {
                Debug.LogError("[GlazingInsulationController] 不能应用空的玻璃隔声数据。", this);
                return false;
            }

            CurrentGlazing = selectedGlazing;
            return ApplyCombinedInsulation();
        }

        /// <summary>
        /// 应用墙、顶、地面等普通表面的隔声预设。只在材质确认后调用，避免预览和取消操作污染声音状态。
        /// </summary>
        public bool ApplySurface(SurfaceCategory category, SurfaceInsulationData selectedSurface)
        {
            if (selectedSurface == null)
            {
                Debug.LogError("[GlazingInsulationController] 不能应用空的表面隔声数据。", this);
                return false;
            }

            if (selectedSurface.category != category)
            {
                Debug.LogError("[GlazingInsulationController] 表面隔声数据分类与当前材质分类不一致。", this);
                return false;
            }

            SetCurrentSurface(selectedSurface, true);
            return ApplyCombinedInsulation();
        }

        private bool ApplyCombinedInsulation()
        {
            if (trafficMixer == null)
            {
                Debug.LogError("[GlazingInsulationController] Traffic Audio Mixer 未绑定。", this);
                return false;
            }

            float weightedVolume = 0f;
            float totalWeight = 0f;
            float weightedCutoff = 0f;
            float cutoffWeight = 0f;

            // 组合模型：每个表面贡献一部分外界声音，transmittedVolume 仍按 glazing 原理转换为 Mixer dB。
            AccumulateGlazing(CurrentGlazing, glazingContributionWeight, ref weightedVolume, ref totalWeight, ref weightedCutoff, ref cutoffWeight);
            AccumulateSurface(CurrentWall, wallContributionWeight, ref weightedVolume, ref totalWeight, ref weightedCutoff, ref cutoffWeight);
            AccumulateSurface(CurrentCeiling, ceilingContributionWeight, ref weightedVolume, ref totalWeight, ref weightedCutoff, ref cutoffWeight);
            AccumulateSurface(CurrentFloor, floorContributionWeight, ref weightedVolume, ref totalWeight, ref weightedCutoff, ref cutoffWeight);

            if (totalWeight <= 0f)
            {
                Debug.LogError("[GlazingInsulationController] 没有可用的隔声预设参与外界声音计算。", this);
                return false;
            }

            float linearVolume = Mathf.Clamp(weightedVolume / totalWeight, MinimumLinearVolume, 1f);
            float mixerVolumeDb = 20f * Mathf.Log10(linearVolume);
            float cutoff = cutoffWeight > 0f ? Mathf.Clamp(weightedCutoff / cutoffWeight, 10f, 22000f) : 22000f;

            bool volumeApplied = trafficMixer.SetFloat(TrafficVolumeParameter, mixerVolumeDb);
            bool lowPassApplied = trafficMixer.SetFloat(TrafficLowPassParameter, cutoff);

            if (!volumeApplied || !lowPassApplied)
            {
                Debug.LogError("[GlazingInsulationController] Audio Mixer 暴露参数缺失或名称不匹配。", this);
                return false;
            }

            return true;
        }

        private void SetCurrentSurface(SurfaceInsulationData data, bool logUnsupportedCategory)
        {
            if (data.category == SurfaceCategory.Wall)
                CurrentWall = data;
            else if (data.category == SurfaceCategory.Ceiling)
                CurrentCeiling = data;
            else if (data.category == SurfaceCategory.Floor)
                CurrentFloor = data;
            else if (logUnsupportedCategory)
                Debug.LogError("[GlazingInsulationController] 普通表面隔声数据不应使用 Glazing 分类。", this);
        }

        private void AccumulateGlazing(
            GlazingAcousticData data,
            float contributionWeight,
            ref float weightedVolume,
            ref float totalWeight,
            ref float weightedCutoff,
            ref float cutoffWeight)
        {
            if (data == null || contributionWeight <= 0f) return;

            float volume = Mathf.Clamp(data.transmittedVolume, MinimumLinearVolume, 1f);
            float audibleWeight = contributionWeight * volume;
            weightedVolume += contributionWeight * volume;
            totalWeight += contributionWeight;
            weightedCutoff += audibleWeight * Mathf.Clamp(data.lowPassCutoff, 10f, 22000f);
            cutoffWeight += audibleWeight;
        }

        private void AccumulateSurface(
            SurfaceInsulationData data,
            float contributionWeight,
            ref float weightedVolume,
            ref float totalWeight,
            ref float weightedCutoff,
            ref float cutoffWeight)
        {
            if (data == null || contributionWeight <= 0f) return;

            float volume = Mathf.Clamp(data.transmittedVolume, MinimumLinearVolume, 1f);
            float audibleWeight = contributionWeight * volume;
            weightedVolume += contributionWeight * volume;
            totalWeight += contributionWeight;
            weightedCutoff += audibleWeight * Mathf.Clamp(data.lowPassCutoff, 10f, 22000f);
            cutoffWeight += audibleWeight;
        }
    }
}
