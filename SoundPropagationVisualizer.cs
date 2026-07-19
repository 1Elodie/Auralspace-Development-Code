using System;
using System.Collections.Generic;
using UnityEngine;

namespace AcousticVisualization
{
    /// <summary>
    /// 在 Unity 中复现 Grasshopper Process Slider 的 0-1 声音传播动画。
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SoundPropagationVisualizer : MonoBehaviour
    {
        [Header("Grasshopper 数据")]
        [SerializeField, Tooltip("由 unnamed.gh 的 Visualize Pachyderm Rays 输出生成的 JSON 数据。")]
        private TextAsset rayData;

        [Header("播放")]
        [SerializeField, Min(0.1f), Tooltip("Process 从 0 播放到 1 所需的秒数。")]
        private float duration = 8f;

        [SerializeField, Tooltip("启用对象时是否自动开始传播动画。")]
        private bool playOnEnable = true;

        [SerializeField, Tooltip("传播到 Process=1 后是否从 0 重新开始。")]
        private bool loop = true;

        [SerializeField, Range(0f, 1f), Tooltip("当前传播进度，与 Grasshopper 的 Process Slider 一致。")]
        private float process;

        [Header("显示")]
        [SerializeField, Min(0.001f), Tooltip("声波前沿点的世界空间直径，单位为米。")]
        private float particleSize = 0.05f;

        // 每个 Grasshopper 数据分支单独统计仍在传播的射线数量，以复现原 Gradient 的颜色输入。
        [NonSerialized] private RuntimeBranch[] _branches;

        // 单个粒子数组承载全部声线，避免为 7204 条路径创建独立 GameObject。
        [NonSerialized] private ParticleSystem.Particle[] _particleBuffer;

        [NonSerialized] private SoundGradientKeyData[] _gradientKeys;
        [NonSerialized] private ParticleSystem _particleSystem;
        [NonSerialized] private float _maximumDistance;
        [NonSerialized] private bool _initialized;
        [NonSerialized] private bool _isPlaying;

        public float Process => process;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            Initialize();
        }

        private void OnEnable()
        {
            // 编辑器热重载不会可靠保留普通 C# 路径缓存，重新启用时按绑定数据恢复运行态。
            if (!_initialized)
            {
                _particleSystem = GetComponent<ParticleSystem>();
                Initialize();
            }

            if (!_initialized)
            {
                return;
            }

            _isPlaying = playOnEnable;
            RenderProcess();
        }

        private void Update()
        {
            if (!_initialized || !_isPlaying)
            {
                return;
            }

            process += Time.deltaTime / duration;
            if (process >= 1f)
            {
                if (loop)
                {
                    process -= Mathf.Floor(process);
                }
                else
                {
                    process = 1f;
                    _isPlaying = false;
                }
            }

            RenderProcess();
        }

        /// <summary>从当前进度继续播放。</summary>
        public void Play()
        {
            _isPlaying = true;
        }

        /// <summary>暂停并保留当前可视化画面。</summary>
        public void Pause()
        {
            _isPlaying = false;
        }

        /// <summary>将传播进度归零并立即重新播放。</summary>
        public void Restart()
        {
            process = 0f;
            _isPlaying = true;
            RenderProcess();
        }

        /// <summary>设置与 Grasshopper Process Slider 对应的进度，供后续 VR UI 直接调用。</summary>
        public void SetProcess(float value)
        {
            process = Mathf.Clamp01(value);
            RenderProcess();
        }

        private void Initialize()
        {
            if (rayData == null)
            {
                Debug.LogError("[SoundPropagationVisualizer] 未绑定 Grasshopper 声线路径 JSON。", this);
                return;
            }

            // 重建前清空派生状态，避免编辑器热重载后把旧的最长距离或粒子缓存带入新数据。
            _maximumDistance = 0f;
            _branches = null;
            _particleBuffer = null;

            SoundRayExportData exportData = JsonUtility.FromJson<SoundRayExportData>(rayData.text);
            if (exportData == null || exportData.branches == null || exportData.branches.Length == 0)
            {
                Debug.LogError("[SoundPropagationVisualizer] 声线路径 JSON 不包含可用分支。", this);
                return;
            }

            _gradientKeys = exportData.gradient;
            Array.Sort(_gradientKeys, (left, right) => left.time.CompareTo(right.time));

            var runtimeBranches = new List<RuntimeBranch>(exportData.branches.Length);
            int totalRayCount = 0;

            foreach (SoundRayBranchData branchData in exportData.branches)
            {
                if (branchData.rays == null || branchData.rays.Length == 0)
                {
                    continue;
                }

                var paths = new List<SoundRayPath>(branchData.rays.Length);
                foreach (SoundRayData ray in branchData.rays)
                {
                    if (ray.points == null || ray.points.Length < 2)
                    {
                        continue;
                    }

                    var path = new SoundRayPath(ray.points);
                    paths.Add(path);
                    _maximumDistance = Mathf.Max(_maximumDistance, path.Length);
                }

                if (paths.Count > 0)
                {
                    runtimeBranches.Add(new RuntimeBranch(paths.ToArray()));
                    totalRayCount += paths.Count;
                }
            }

            if (totalRayCount == 0 || _gradientKeys == null || _gradientKeys.Length == 0)
            {
                Debug.LogError("[SoundPropagationVisualizer] 声线路径或颜色数据为空。", this);
                return;
            }

            _branches = runtimeBranches.ToArray();
            _particleBuffer = new ParticleSystem.Particle[totalRayCount];

            // 粒子完全由 SetParticles 驱动；暂停内置模拟可防止生命周期改变手动设置的波前画面。
            ParticleSystem.MainModule main = _particleSystem.main;
            main.maxParticles = totalRayCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.loop = false;
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particleSystem.Pause(true);

            _initialized = true;
            Debug.Log($"[SoundPropagationVisualizer] 已载入 {totalRayCount} 条声线，最长传播距离 {_maximumDistance:F2} 米。", this);
        }

        private void RenderProcess()
        {
            if (!_initialized)
            {
                return;
            }

            float distance = process * _maximumDistance;
            int particleIndex = 0;

            foreach (RuntimeBranch branch in _branches)
            {
                int activeCount = 0;
                foreach (SoundRayPath path in branch.Paths)
                {
                    if (distance <= path.Length)
                    {
                        activeCount++;
                    }
                }

                // 原 GH 定义以“当前有效点数”在“初始数量到 1”之间驱动 Gradient，因此颜色会随射线结束而推进。
                float colourFactor = branch.Paths.Length > 1
                    ? (branch.Paths.Length - activeCount) / (branch.Paths.Length - 1f)
                    : 1f;
                Color displayColour = EvaluateGradient(colourFactor);

                foreach (SoundRayPath path in branch.Paths)
                {
                    if (distance > path.Length)
                    {
                        continue;
                    }

                    _particleBuffer[particleIndex] = new ParticleSystem.Particle
                    {
                        position = path.EvaluateDistance(distance),
                        startColor = displayColour,
                        startSize = particleSize,
                        startLifetime = 1f,
                        remainingLifetime = 1f
                    };
                    particleIndex++;
                }
            }

            _particleSystem.SetParticles(_particleBuffer, particleIndex);
        }

        private Color EvaluateGradient(float factor)
        {
            float clampedFactor = Mathf.Clamp01(factor);
            SoundGradientKeyData first = _gradientKeys[0];
            if (clampedFactor <= first.time)
            {
                return ToDisplayColour(first);
            }

            for (int i = 1; i < _gradientKeys.Length; i++)
            {
                SoundGradientKeyData right = _gradientKeys[i];
                if (clampedFactor > right.time)
                {
                    continue;
                }

                SoundGradientKeyData left = _gradientKeys[i - 1];
                float localFactor = Mathf.InverseLerp(left.time, right.time, clampedFactor);
                Color srgbColour = Color.LerpUnclamped(ToSrgbColour(left), ToSrgbColour(right), localFactor);
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgbColour.linear : srgbColour;
            }

            return ToDisplayColour(_gradientKeys[_gradientKeys.Length - 1]);
        }

        private static Color ToDisplayColour(SoundGradientKeyData key)
        {
            Color srgbColour = ToSrgbColour(key);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? srgbColour.linear : srgbColour;
        }

        private static Color ToSrgbColour(SoundGradientKeyData key)
        {
            return new Color(key.r / 255f, key.g / 255f, key.b / 255f, key.a / 255f);
        }

        [Serializable]
        private sealed class RuntimeBranch
        {
            public SoundRayPath[] Paths { get; }

            public RuntimeBranch(SoundRayPath[] paths)
            {
                Paths = paths;
            }
        }
    }
}
