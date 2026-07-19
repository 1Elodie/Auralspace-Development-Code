using System;
using UnityEngine;

namespace AcousticVisualization
{
    /// <summary>
    /// Grasshopper 声线路径导出文件的数据结构。
    /// 字段名称与 JSON 保持一致，避免额外的运行时映射与序列化风险。
    /// </summary>
    [Serializable]
    public sealed class SoundRayExportData
    {
        public string source;
        public string coordinateSystem;
        public float processMin;
        public float processMax;
        public SoundGradientKeyData[] gradient;
        public SoundRayBranchData[] branches;
    }

    [Serializable]
    public sealed class SoundGradientKeyData
    {
        public float time;
        public int r;
        public int g;
        public int b;
        public int a;
    }

    [Serializable]
    public sealed class SoundRayBranchData
    {
        public string path;
        public SoundRayData[] rays;
    }

    [Serializable]
    public sealed class SoundRayData
    {
        public SoundRayPointData[] points;
    }

    [Serializable]
    public sealed class SoundRayPointData
    {
        public float x;
        public float y;
        public float z;
    }

    /// <summary>
    /// 对单条折线路径进行累计长度采样，不依赖 MonoBehaviour，便于复用和独立验证。
    /// </summary>
    public sealed class SoundRayPath
    {
        // Unity 空间中的折线顶点；导入时已完成 Rhino 毫米坐标与轴向转换。
        private readonly Vector3[] _points;

        // 每个顶点距路径起点的累计距离，用于按传播距离快速定位折线段。
        private readonly float[] _cumulativeLengths;

        public float Length { get; }

        public SoundRayPath(SoundRayPointData[] sourcePoints)
        {
            if (sourcePoints == null || sourcePoints.Length < 2)
            {
                throw new ArgumentException("声线路径至少需要两个顶点。", nameof(sourcePoints));
            }

            _points = new Vector3[sourcePoints.Length];
            _cumulativeLengths = new float[sourcePoints.Length];

            for (int i = 0; i < sourcePoints.Length; i++)
            {
                _points[i] = ConvertRhinoPoint(sourcePoints[i]);
                if (i > 0)
                {
                    _cumulativeLengths[i] = _cumulativeLengths[i - 1] +
                                            Vector3.Distance(_points[i - 1], _points[i]);
                }
            }

            Length = _cumulativeLengths[_cumulativeLengths.Length - 1];
        }

        public Vector3 EvaluateDistance(float distance)
        {
            float clampedDistance = Mathf.Clamp(distance, 0f, Length);

            // Pachyderm 当前每条射线最多只有五个顶点，顺序扫描比额外二分结构更直接且开销稳定。
            for (int i = 1; i < _cumulativeLengths.Length; i++)
            {
                if (clampedDistance > _cumulativeLengths[i])
                {
                    continue;
                }

                float segmentStart = _cumulativeLengths[i - 1];
                float segmentLength = _cumulativeLengths[i] - segmentStart;
                float factor = segmentLength > Mathf.Epsilon
                    ? (clampedDistance - segmentStart) / segmentLength
                    : 0f;
                return Vector3.LerpUnclamped(_points[i - 1], _points[i], factor);
            }

            return _points[_points.Length - 1];
        }

        private static Vector3 ConvertRhinoPoint(SoundRayPointData point)
        {
            // 当前教室 FBX 的实际包围盒证明映射为 (-X, Z, -Y)，同时 Rhino 毫米需换算为 Unity 米。
            const float millimetresToMetres = 0.001f;
            return new Vector3(
                -point.x * millimetresToMetres,
                point.z * millimetresToMetres,
                -point.y * millimetresToMetres);
        }
    }
}
