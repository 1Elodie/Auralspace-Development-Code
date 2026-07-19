using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRMaterialSystem
{
    /// <summary>
    /// 世界空间材质选择面板，负责预览、确认、取消以及显示当前材质的声学参数。
    /// </summary>
    public class MaterialSelectionPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text editingLabel;
        [SerializeField] private TMP_Text currentMaterialLabel;
        [SerializeField] private Button closeButton;

        [Header("Options")]
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private GameObject emptyStateMessage;
        [SerializeField] private MaterialOptionButton optionButtonPrefab;

        [Header("Actions")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Material Acoustic Data")]
        [Tooltip("显示所选材质 Meta 反射系数与由其推算的吸声系数。")]
        [SerializeField] private GameObject acousticDataRoot;
        [SerializeField] private TMP_Text acousticDataText;
        [SerializeField] private Button closeAcousticDataButton;

        [Header("Placement")]
        [SerializeField] private VRPanelPlacement placement;

        [Header("Outdoor Sound Insulation")]
        [Tooltip("材质确认后，将墙、顶、地面或玻璃的隔声预设应用到外界声音。")]
        [SerializeField] private GlazingInsulationController glazingInsulationController;

        [Header("Indoor Shoebox Acoustics")]
        [Tooltip("材质确认后更新 Meta Shoebox 房间对应表面；预览和取消不会改变混响。")]
        [SerializeField] private ShoeboxRoomAcousticsController roomAcousticsController;

        /// <summary>面板关闭时通知管理器清除当前表面选择。</summary>
        public event Action CloseRequested;

        public bool IsOpen => gameObject.activeSelf;

        private EditableSurface _surface;
        private readonly List<MaterialOptionButton> _spawned = new List<MaterialOptionButton>();
        private AcousticMaterialData _previewSelection;

        private void Awake()
        {
            // 面板在场景中默认禁用，首次 Open 激活时才执行 Awake，因此此处不能再次禁用自身。
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
            if (closeAcousticDataButton != null)
                closeAcousticDataButton.onClick.AddListener(() => acousticDataRoot.SetActive(false));
        }

        public void Open(EditableSurface surface, IReadOnlyList<AcousticMaterialData> options, Camera viewer)
        {
            _surface = surface;
            _previewSelection = null;

            gameObject.SetActive(true);
            if (placement != null && viewer != null) placement.PlaceInFront(viewer.transform);

            UpdateHeader();
            BuildOptions(options);
            UpdateConfirmInteractable();
            SetStatus(string.Empty);
            if (acousticDataRoot != null) acousticDataRoot.SetActive(false);
        }

        private void UpdateHeader()
        {
            if (editingLabel != null)
                editingLabel.text = "Editing: " + (_surface != null ? _surface.Label : "-");
            if (currentMaterialLabel != null)
                currentMaterialLabel.text = "Current Material: " + (_surface != null ? _surface.CurrentMaterialName : "-");
        }

        private void BuildOptions(IReadOnlyList<AcousticMaterialData> options)
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            _spawned.Clear();

            int count = options?.Count ?? 0;
            if (emptyStateMessage != null) emptyStateMessage.SetActive(count == 0);
            if (count == 0) return;

            if (optionButtonPrefab == null || optionsContainer == null)
            {
                Debug.LogWarning("[MaterialSelectionPanel] 材质按钮预制体或容器未绑定，无法生成选项。", this);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                AcousticMaterialData data = options[i];
                if (data == null) continue;

                MaterialOptionButton optionButton = Instantiate(optionButtonPrefab, optionsContainer);
                optionButton.gameObject.SetActive(true);
                optionButton.Setup(data);
                optionButton.Clicked += OnOptionClicked;
                optionButton.DataRequested += OnDataRequested;
                _spawned.Add(optionButton);
            }
        }

        private void OnOptionClicked(AcousticMaterialData data)
        {
            if (acousticDataRoot != null) acousticDataRoot.SetActive(false);
            _previewSelection = data;
            if (_surface != null) _surface.ApplyPreview(data != null ? data.visualMaterial : null);

            if (currentMaterialLabel != null && data != null)
                currentMaterialLabel.text = "Current Material: " + data.ResolvedName + " (preview)";

            UpdateConfirmInteractable();
            SetStatus(string.Empty);
        }

        private void OnDataRequested(AcousticMaterialData data)
        {
            if (data == null || acousticDataText == null) return;

            // Meta 表格给出反射系数；界面吸声值按 α=1-r² 推算，并明确标注为估算值。
            float[] reflection = data.GetMetaReflectionBands();
            acousticDataText.text = string.Format(
                "{0}\nMeta preset: {1}\n" +
                "Reflection r\n0–176: {2:F3}   176–775: {3:F3}\n775–3408: {4:F3}   3408–22050 Hz: {5:F3}\n" +
                "Estimated absorption α = 1-r²\n125: {6:F2}  250: {7:F2}  500: {8:F2}\n1k: {9:F2}  2k: {10:F2}  4k: {11:F2}\nSource: Meta XR Audio SDK official preset table",
                data.ResolvedName,
                data.metaRoomMaterialPreset,
                reflection[0], reflection[1], reflection[2], reflection[3],
                data.absorption125Hz, data.absorption250Hz, data.absorption500Hz,
                data.absorption1000Hz, data.absorption2000Hz, data.absorption4000Hz);

            if (acousticDataRoot != null) acousticDataRoot.SetActive(true);
        }

        private void OnConfirm()
        {
            if (_surface == null || _previewSelection == null) return;

            // 确认时记录语义材质名，即使多个选项共用贴图，界面也能显示真正选中的材料。
            _surface.ConfirmPreview(_previewSelection.ResolvedName);

            // 只有确认后才更新室外隔声，取消预览不会污染已确认状态。
            if (_previewSelection.surfaceInsulationData != null)
            {
                if (glazingInsulationController != null)
                    glazingInsulationController.ApplySurface(_surface.Category, _previewSelection.surfaceInsulationData);
                else
                    Debug.LogError("[MaterialSelectionPanel] GlazingInsulationController 未绑定。", this);
            }

            // 保留旧玻璃数据入口，避免已有 GlazingAcousticData 资源和场景绑定失效。
            if (_surface.Category == SurfaceCategory.Glazing && _previewSelection.glazingAcousticData != null)
            {
                if (glazingInsulationController != null)
                    glazingInsulationController.ApplyGlazing(_previewSelection.glazingAcousticData);
                else
                    Debug.LogError("[MaterialSelectionPanel] GlazingInsulationController 未绑定。", this);
            }

            // Shoebox 更新与视觉材质共用同一次确认，保证用户看到和听到的是同一材料状态。
            if (roomAcousticsController != null)
                roomAcousticsController.ApplyMaterial(_surface.Category, _previewSelection);
            else
                Debug.LogError("[MaterialSelectionPanel] ShoeboxRoomAcousticsController 未绑定。", this);

            UpdateHeader();
            SetStatus("Material Applied");
            _surface.FlashSuccess();
        }

        private void OnCancel()
        {
            if (_surface != null) _surface.RestoreConfirmed();
            _previewSelection = null;
            UpdateHeader();
            UpdateConfirmInteractable();
            SetStatus("Reverted");
        }

        private void OnClose()
        {
            if (_surface != null && _surface.HasUnconfirmedPreview)
                _surface.RestoreConfirmed();

            _previewSelection = null;
            CloseRequested?.Invoke();
            Close();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            _surface = null;
        }

        private void UpdateConfirmInteractable()
        {
            if (confirmButton != null)
                confirmButton.interactable = _surface != null && _previewSelection != null;
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null) statusLabel.text = value;
        }
    }
}
