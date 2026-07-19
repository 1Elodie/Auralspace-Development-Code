using UnityEngine;

namespace VRMaterialSystem
{
    /// <summary>
    /// 可选择材质的数据容器，同时保存视觉材质、隔声数据与 Meta Shoebox 房间材质预设。
    /// </summary>
    [CreateAssetMenu(fileName = "AcousticMaterialData", menuName = "VR Material System/Acoustic Material Data")]
    public class AcousticMaterialData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public SurfaceCategory category;

        [Tooltip("Optional. May be null until a real Unity Material is assigned.")]
        public Material visualMaterial;

        [Tooltip("Optional preview thumbnail. May be null.")]
        public Sprite thumbnail;

        [Header("Surface Sound Insulation")]
        [Tooltip("墙、顶、地面等建筑表面使用。确认该材质后，会把对应隔声参数纳入外界声音计算。")]
        public SurfaceInsulationData surfaceInsulationData;

        [Tooltip("兼容旧玻璃系统，仅玻璃选项使用。")]
        public GlazingAcousticData glazingAcousticData;

        [Header("Meta Shoebox Room Acoustics")]
        [Tooltip("确认材质后应用到 Meta XR Audio Shoebox 对应房间表面的官方材质预设。")]
        public MetaXRAudioRoomAcousticProperties.MaterialPreset metaRoomMaterialPreset;

        [Header("Sound Absorption Coefficients (per octave band)")]
        public float absorption125Hz;
        public float absorption250Hz;
        public float absorption500Hz;
        public float absorption1000Hz;
        public float absorption2000Hz;
        public float absorption4000Hz;

        [Header("Physical / Source")]
        public float thicknessMm;
        public string mountingCondition;
        public string dataSource;
        [TextArea] public string description;

        [Header("Glazing Acoustic Ratings")]
        public float stc;
        public float oitc;

        /// <summary>界面显示名为空时使用资源名，避免选项出现空白。</summary>
        public string ResolvedName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>读取 Meta 官方四频段反射系数，频段边界由 SDK 固定。</summary>
        public float[] GetMetaReflectionBands()
        {
            return MetaXRAudioRoomAcousticProperties.GetMaterialPresetBands(metaRoomMaterialPreset);
        }
    }
}
