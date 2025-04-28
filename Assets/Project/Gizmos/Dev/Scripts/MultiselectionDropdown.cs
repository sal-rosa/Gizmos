namespace Gizmos
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [RequireComponent(typeof(RectTransform))]
    public class MultiselectionDropdown : Selectable,
        IPointerClickHandler, ISubmitHandler, ICancelHandler, IEventSystemHandler
    {
        public GameObject OptionRect;

        public GameObject HeadingTextPrefab;

        public GameObject MultiselectionItemPrefab;

        public Text CaptionText;

        public int TextLimit = 20;

        public Action OnValueChanged;

        private float _itemHeight;
        private float _maxHeight;
        private bool _optionChanged = true;

        private List<OptionData> _options = new List<OptionData>();
        private List<Toggle> _optionToggles = new List<Toggle>();

        public List<OptionData> Options
        {
            get
            {
                return _options;
            }

            set
            {
                if (CaptionText != null)
                {
                    CaptionText.text = "Selecionar";
                }

                _optionToggles.Clear();
                _optionChanged = true;
                _options = value;
                if (_options.Count == 0)
                {
                    CaptionText.text = "Nenhuma opção disponível";
                }
            }
        }

        public List<int> SelectedValues
        {
            get
            {
                List<int> index = new List<int>();
                for (int i = 0; i < _optionToggles.Count; i++)
                {
                    if (_optionToggles[i].isOn)
                    {
                        index.Add(i);
                    }
                }

                return index;
            }
        }

        public void Deselect()
        {
            if (OptionRect.activeSelf)
            {
                OptionRect.SetActive(false);
            }
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            bool currentActive = OptionRect.activeSelf;
            if (!currentActive)
            {
                UpdateOptionRect();
                OptionRect.SetActive(!currentActive);
            }
            else
            {
                if (eventData.pointerCurrentRaycast.gameObject.GetHashCode() !=
                    OptionRect.GetHashCode())
                {
                    OptionRect.SetActive(false);
                }
            }
        }

        void ISubmitHandler.OnSubmit(BaseEventData eventData)
        {
            if (OnValueChanged != null)
            {
                OnValueChanged();
            }

            Deselect();
        }

        void ICancelHandler.OnCancel(BaseEventData eventData)
        {
            Deselect();
        }

        protected override void Awake()
        {
            _maxHeight = OptionRect.GetComponent<RectTransform>().rect.height;
            _itemHeight = OptionRect.GetComponent<ScrollRect>()
                .content.GetComponent<RectTransform>().rect.height;
            _optionChanged = true;
        }

        private void OnSelectionChanged(bool isSelected)
        {
            List<string> selectedOptions = new List<string>();
            foreach (var toggle in _optionToggles)
            {
                if (toggle.isOn)
                {
                    selectedOptions.Add(toggle.GetComponent<DoubleLabelsItem>().FirstLabel.text);
                }
            }

            if (CaptionText != null && _options.Count > 0)
            {
                if (selectedOptions.Count == 0)
                {
                    CaptionText.text = "Selecionar";
                }
                else
                {
                    string combined = string.Join(",", selectedOptions.ToArray());
                    if (TextLimit > 0 && combined.Length > TextLimit)
                    {
                        combined = combined.Substring(0, TextLimit) + "...";
                    }

                    CaptionText.text = combined;
                }
            }

            if (OnValueChanged != null)
            {
                OnValueChanged();
            }
        }

        private void UpdateOptionRect()
        {
            if (!_optionChanged)
            {
                return;
            }

            RectTransform optionRect = OptionRect.GetComponent<RectTransform>();
            RectTransform contentRect = OptionRect.GetComponent<ScrollRect>().content;

            _optionToggles.Clear();
            contentRect.transform.DetachChildren();

            int count = 0;
            if (HeadingTextPrefab != null && _options.Count > 0)
            {
                GameObject headingText = Instantiate(HeadingTextPrefab);
                headingText.transform.SetParent(contentRect.transform, false);
                count++;
            }

            foreach (var optionData in Options)
            {
                GameObject selectableItem = Instantiate(MultiselectionItemPrefab);
                selectableItem.transform.SetParent(contentRect.transform, false);
                selectableItem.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(0, -(_itemHeight * count));

                selectableItem.GetComponent<DoubleLabelsItem>()
                    .SetLabels(optionData.MajorInfo, optionData.MinorInfo);

                var toggle = selectableItem.GetComponent<Toggle>();
                toggle.onValueChanged.AddListener(OnSelectionChanged);
                _optionToggles.Add(toggle);

                count++;
            }

            optionRect.sizeDelta =
                new Vector2(optionRect.sizeDelta.x, Mathf.Min(count * _itemHeight, _maxHeight));
            contentRect.sizeDelta =
                new Vector2(contentRect.sizeDelta.x, count * _itemHeight);
            CaptionText.text = _options.Count == 0 ? "Nenhuma opção disponivel" : "Selecionar";
            _optionChanged = false;
        }

        [Serializable]
        public class OptionData
        {
            [SerializeField]
            public string MajorInfo;

            [SerializeField]
            public string MinorInfo;

            public OptionData(string major, string minor)
            {
                MajorInfo = major;
                MinorInfo = minor;
            }
        }
    }
}
