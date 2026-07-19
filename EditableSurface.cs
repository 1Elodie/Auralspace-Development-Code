using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRMaterialSystem
{
    /// <summary>
    /// Represents one editable architectural surface group (e.g. all renderers under "Wall").
    /// Wraps the XR interactable on the same GameObject, manages confirmed/preview materials
    /// for the whole group, and drives the SurfaceHighlighter. It NEVER edits the imported
    /// FBX asset - it only changes the live scene instance renderers.
    /// </summary>
    [RequireComponent(typeof(SurfaceHighlighter))]
    public class EditableSurface : MonoBehaviour
    {
        [SerializeField] private SurfaceCategory category = SurfaceCategory.Wall;
        [SerializeField] private string label = "Surface";

        [Tooltip("All renderers that make up this editable group. Auto-collected from children if empty.")]
        [SerializeField] private List<Renderer> groupRenderers = new List<Renderer>();

        public SurfaceCategory Category => category;
        public string Label => label;
        public bool HasUnconfirmedPreview => _hasPreview;

        // Plain C# events the SurfaceSelectionManager subscribes to (keeps manager XR-agnostic).
        public event Action<EditableSurface> HoverEntered;
        public event Action<EditableSurface> HoverExited;
        public event Action<EditableSurface> SelectEntered;

        private SurfaceHighlighter _highlighter;
        private XRBaseInteractable _interactable;

        // Per-renderer snapshot of the currently confirmed (committed) materials.
        private readonly List<Material[]> _confirmed = new List<Material[]>();
        private string _confirmedMaterialName;
        private bool _hasPreview;

        private void Awake()
        {
            _highlighter = GetComponent<SurfaceHighlighter>();
            _interactable = GetComponent<XRBaseInteractable>();

            if (groupRenderers == null || groupRenderers.Count == 0)
                CollectRenderers();

            // Capture the original (imported) materials as the initial confirmed state.
            _confirmed.Clear();
            foreach (var r in groupRenderers)
                _confirmed.Add(r != null ? (Material[])r.sharedMaterials.Clone() : null);

            _confirmedMaterialName = GetFirstConfirmedMaterialAssetName();

            if (_highlighter != null)
                _highlighter.Initialize(groupRenderers);

            if (_interactable == null)
                Debug.LogWarning($"[EditableSurface] No XRBaseInteractable on '{name}'. XR hover/select will not work until one is added.", this);
        }

        private void OnEnable()
        {
            if (_interactable == null) return;
            _interactable.hoverEntered.AddListener(OnXrHoverEntered);
            _interactable.hoverExited.AddListener(OnXrHoverExited);
            _interactable.selectEntered.AddListener(OnXrSelectEntered);
        }

        private void OnDisable()
        {
            if (_interactable == null) return;
            _interactable.hoverEntered.RemoveListener(OnXrHoverEntered);
            _interactable.hoverExited.RemoveListener(OnXrHoverExited);
            _interactable.selectEntered.RemoveListener(OnXrSelectEntered);
        }

        private void OnXrHoverEntered(HoverEnterEventArgs args) => HoverEntered?.Invoke(this);
        private void OnXrHoverExited(HoverExitEventArgs args) => HoverExited?.Invoke(this);
        private void OnXrSelectEntered(SelectEnterEventArgs args) => SelectEntered?.Invoke(this);

        private void CollectRenderers()
        {
            groupRenderers = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<OutlineMarker>() != null) continue; // skip outline clones
                groupRenderers.Add(r);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && (groupRenderers == null || groupRenderers.Count == 0))
                CollectRenderers();
        }

        // ---- Highlight control (called by the manager) ----
        public void ShowHover() => _highlighter?.ShowHover();
        public void ShowSelected() => _highlighter?.ShowSelected();
        public void ClearHighlight() => _highlighter?.Clear();
        public void FlashSuccess() => _highlighter?.FlashSuccess();

        /// <summary>Name of the first confirmed material, shown in the panel header.</summary>
        public string CurrentMaterialName
        {
            get
            {
                // 墙体多个选项可能共用同一个贴图/Unity Material，因此 UI 优先显示“已确认的选项名”，而不是 Renderer 材质资源名。
                if (!string.IsNullOrEmpty(_confirmedMaterialName))
                    return _confirmedMaterialName;

                string fallbackName = GetFirstConfirmedMaterialAssetName();
                if (!string.IsNullOrEmpty(fallbackName))
                    return fallbackName;

                return "(none)";
            }
        }

        /// <summary>Live, non-destructive preview. Applies the material to every renderer slot in the group.</summary>
        public void ApplyPreview(Material mat)
        {
            if (mat == null)
            {
                Debug.LogWarning($"[EditableSurface] Preview material is null for '{label}'. Assign AcousticMaterialData.visualMaterial to preview.", this);
                return;
            }

            for (int i = 0; i < groupRenderers.Count; i++)
            {
                var r = groupRenderers[i];
                if (r == null) continue;
                var arr = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int s = 0; s < arr.Length; s++) arr[s] = mat;
                r.sharedMaterials = arr;
            }
            _hasPreview = true;
        }

        /// <summary>Commits the current preview as the confirmed state.</summary>
        public void ConfirmPreview()
        {
            ConfirmPreview(null);
        }

        /// <summary>Commits the current preview and records the user-facing material option name for UI display.</summary>
        public void ConfirmPreview(string confirmedMaterialName)
        {
            if (!_hasPreview) return;
            for (int i = 0; i < groupRenderers.Count; i++)
            {
                var r = groupRenderers[i];
                _confirmed[i] = r != null ? (Material[])r.sharedMaterials.Clone() : null;
            }

            // 确认后保存“选项名”，用于区分共用同一贴图的 Concrete / Brick / Acoustic panel 等语义材料。
            if (!string.IsNullOrEmpty(confirmedMaterialName))
                _confirmedMaterialName = confirmedMaterialName;

            _hasPreview = false;
        }

        /// <summary>Reverts all renderers to the last confirmed materials.</summary>
        public void RestoreConfirmed()
        {
            for (int i = 0; i < groupRenderers.Count && i < _confirmed.Count; i++)
            {
                var r = groupRenderers[i];
                if (r == null || _confirmed[i] == null) continue;
                r.sharedMaterials = _confirmed[i];
            }
            _hasPreview = false;
        }

        /// <summary>Editor/setup helper to assign category + label from automation.</summary>
        public void Configure(SurfaceCategory cat, string lbl)
        {
            category = cat;
            label = lbl;
        }

        private string GetFirstConfirmedMaterialAssetName()
        {
            if (_confirmed.Count > 0 && _confirmed[0] != null && _confirmed[0].Length > 0 && _confirmed[0][0] != null)
                return _confirmed[0][0].name;

            return null;
        }
    }
}
