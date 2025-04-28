namespace Gizmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEngine;
    using UnityEngine.UI;

    public class ResolveMenuManager : MonoBehaviour
    {
        public PersistentCloudAnchorsController Controller;

        public MultiselectionDropdown Multiselection;

        public InputField InputField;

        public GameObject InvalidInputWarning;

        public Button ResolveButton;

        private CloudAnchorHistoryCollection _history = new CloudAnchorHistoryCollection();

        private Color _activeColor;

        public void OnInputFieldValueChanged(string inputString)
        {
            var regex = new Regex("^[a-zA-Z0-9-_,]*$");
            InvalidInputWarning.SetActive(!regex.IsMatch(inputString));
        }

        public void OnInputFieldEndEdit(string inputString)
        {
            if (InvalidInputWarning.activeSelf)
            {
                return;
            }

            OnResolvingSelectionChanged();
        }

        public void OnResolvingSelectionChanged()
        {
            Controller.ResolvingSet.Clear();

            List<int> selectedIndex = Multiselection.SelectedValues;
            if (selectedIndex.Count > 0)
            {
                foreach (int index in selectedIndex)
                {
                    Controller.ResolvingSet.Add(_history.Collection[index].Id);
                }
            }

            if (!InvalidInputWarning.activeSelf && InputField.text.Length > 0)
            {
                string[] inputIds = InputField.text.Split(',');
                if (inputIds.Length > 0)
                {
                    Controller.ResolvingSet.UnionWith(inputIds);
                }
            }

            SetButtonActive(ResolveButton, Controller.ResolvingSet.Count > 0);
        }

        public void Awake()
        {
            _activeColor = ResolveButton.GetComponent<Image>().color;
        }

        public void OnEnable()
        {
            SetButtonActive(ResolveButton, false);
            InvalidInputWarning.SetActive(false);
            InputField.text = string.Empty;
            _history = Controller.LoadCloudAnchorHistory();

            Multiselection.OnValueChanged += OnResolvingSelectionChanged;
            var options = new List<MultiselectionDropdown.OptionData>();
            foreach (var data in _history.Collection)
            {
                options.Add(new MultiselectionDropdown.OptionData(
                    data.Name, FormatDateTime(data.CreatedTime)));
            }

            Multiselection.Options = options;
        }

        public void OnDisable()
        {
            Multiselection.OnValueChanged -= OnResolvingSelectionChanged;
            Multiselection.Deselect();
            Multiselection.Options.Clear();
            _history.Collection.Clear();
        }

        private string FormatDateTime(DateTime time)
        {
            TimeSpan span = DateTime.Now.Subtract(time);
            return span.Hours == 0 ? span.Minutes == 0 ? "Agora" :
                string.Format("{0}m atrás", span.Minutes) : string.Format("{0}h atrás", span.Hours);
        }

        private void SetButtonActive(Button button, bool active)
        {
            button.GetComponent<Image>().color = active ? _activeColor : Color.grey;
            button.enabled = active;
        }
    }
}
