using UnityEngine;

namespace VRMaterialSystem
{
    /// <summary>
    /// 将已确认的建筑表面材质映射到 Meta XR Audio Shoebox 六个房间表面。
    /// 当前房间近似约定：墙体控制前/后面，玻璃控制左/右面，地面与顶面各自独立。
    /// </summary>
    public sealed class ShoeboxRoomAcousticsController : MonoBehaviour
    {
        [Tooltip("场景中唯一的 Meta Shoebox 房间组件；多个房间组件会产生未定义行为。")]
        [SerializeField] private MetaXRAudioRoomAcousticProperties roomProperties;

        public MetaXRAudioRoomAcousticProperties RoomProperties => roomProperties;

        public void ApplyMaterial(SurfaceCategory category, AcousticMaterialData materialData)
        {
            if (roomProperties == null || materialData == null)
            {
                Debug.LogError("[ShoeboxRoomAcousticsController] 房间组件或材质数据未绑定。", this);
                return;
            }

            MetaXRAudioRoomAcousticProperties.MaterialPreset preset = materialData.metaRoomMaterialPreset;

            // Shoebox 只能描述矩形六面，因此把同类实际表面同步到已确认的近似房间面。
            switch (category)
            {
                case SurfaceCategory.Ceiling:
                    roomProperties.ceilingMaterial = preset;
                    break;
                case SurfaceCategory.Floor:
                    roomProperties.floorMaterial = preset;
                    break;
                case SurfaceCategory.Wall:
                    roomProperties.frontMaterial = preset;
                    roomProperties.backMaterial = preset;
                    break;
                case SurfaceCategory.Glazing:
                    roomProperties.leftMaterial = preset;
                    roomProperties.rightMaterial = preset;
                    break;
            }
        }
    }
}
