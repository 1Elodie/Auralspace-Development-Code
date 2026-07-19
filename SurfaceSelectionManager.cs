using System.Collections.Generic;
using UnityEngine;

namespace VRMaterialSystem
{
    /// <summary>
    /// Central coordinator. Listens to hover/select events from every EditableSurface, tracks the
    /// hovered/selected surface, blocks new environment selection while the panel is open, and
    /// opens/closes the material panel. Holds the four (initially empty) material collections that
    /// designers fill in the Inspector.
    /// </summary>
    public class SurfaceSelectionManager : MonoBehaviour
    {
        [Header("Surfaces (auto-found at runtime if left empty)")]
        [SerializeField] private List<EditableSurface> surfaces = new List<EditableSurface>();

        [Header("Panel")]
        [SerializeField] private MaterialSelectionPanel panel;
        [SerializeField] private Camera viewerCamera;

        [Header("Material Collections (empty by default - fill in the Inspector)")]
        [SerializeField] private List<AcousticMaterialData> ceilingMaterials = new List<AcousticMaterialData>();
        [SerializeField] private List<AcousticMaterialData> floorMaterials = new List<AcousticMaterialData>();
        [SerializeField] private List<AcousticMaterialData> wallMaterials = new List<AcousticMaterialData>();
        [UnityEngine.Serialization.FormerlySerializedAs("windowGlassMaterials")]
        [SerializeField] private List<AcousticMaterialData> glazingMaterials = new List<AcousticMaterialData>();

        private EditableSurface _hovered;
        private EditableSurface _selected;

        public bool PanelOpen => panel != null && panel.IsOpen;

        private void Awake()
        {
            if (surfaces == null || surfaces.Count == 0)
                surfaces = new List<EditableSurface>(FindObjectsByType<EditableSurface>(FindObjectsSortMode.None));

            if (viewerCamera == null) viewerCamera = Camera.main;
        }

        private void OnEnable()
        {
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                s.HoverEntered += OnSurfaceHoverEntered;
                s.HoverExited += OnSurfaceHoverExited;
                s.SelectEntered += OnSurfaceSelectEntered;
            }
            if (panel != null) panel.CloseRequested += OnPanelClosed;
        }

        private void OnDisable()
        {
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                s.HoverEntered -= OnSurfaceHoverEntered;
                s.HoverExited -= OnSurfaceHoverExited;
                s.SelectEntered -= OnSurfaceSelectEntered;
            }
            if (panel != null) panel.CloseRequested -= OnPanelClosed;
        }

        private void OnSurfaceHoverEntered(EditableSurface s)
        {
            if (PanelOpen) return;          // no hover changes while editing
            _hovered = s;
            if (s != _selected) s.ShowHover();
        }

        private void OnSurfaceHoverExited(EditableSurface s)
        {
            if (PanelOpen) return;
            if (_hovered == s) _hovered = null;
            if (s != _selected) s.ClearHighlight();
        }

        private void OnSurfaceSelectEntered(EditableSurface s)
        {
            if (PanelOpen) return;          // locked - cannot pick another surface while panel open
            _selected = s;
            s.ShowSelected();

            var list = GetMaterials(s.Category);
            if (panel != null)
                panel.Open(s, list, viewerCamera);
            else
                Debug.LogWarning("[SurfaceSelectionManager] No MaterialSelectionPanel assigned.", this);
        }

        private void OnPanelClosed()
        {
            if (_selected != null)
            {
                _selected.ClearHighlight();
                _selected = null;
            }
            _hovered = null;
        }

        private IReadOnlyList<AcousticMaterialData> GetMaterials(SurfaceCategory category)
        {
            switch (category)
            {
                case SurfaceCategory.Ceiling: return ceilingMaterials;
                case SurfaceCategory.Floor: return floorMaterials;
                case SurfaceCategory.Wall: return wallMaterials;
                case SurfaceCategory.Glazing: return glazingMaterials;
                default: return System.Array.Empty<AcousticMaterialData>();
            }
        }
    }
}
