using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows;

namespace OllamaVision
{
    /// <summary>
    /// A serializable representation of a UI element's key properties.
    /// </summary>
    public class UIElementInfo
    {
        public string Name { get; set; }
        public string ControlType { get; set; }
        public string AutomationId { get; set; }
        public Rect BoundingRectangle { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// Provides services for interacting with the UI using Microsoft UI Automation.
    /// </summary>
    public class UIAutomationService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Gets a structured map of all interactive UI elements in the current foreground window.
        /// </summary>
        public List<UIElementInfo> GetUIMapForForegroundWindow()
        {
            var element = AutomationElement.FromHandle(GetForegroundWindow());
            var elementList = new List<UIElementInfo>();
            if (element == null) return elementList;

            AddElementAndChildren(element, elementList);
            return elementList;
        }

        /// <summary>
        /// Finds a UI element based on the AI's description and executes a specified action on it.
        /// </summary>
        public bool FindAndExecuteAction(AIAction action)
        {
            var rootElement = AutomationElement.FromHandle(GetForegroundWindow());
            if (rootElement == null || action.Control == null) return false;

            var condition = BuildConditionFromIdentifier(action.Control);
            var targetElement = rootElement.FindFirst(TreeScope.Descendants, condition);

            if (targetElement == null) return false;

            try
            {
                switch (action.Action.ToUpper())
                {
                    case "INVOKE":
                        if (targetElement.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
                        {
                            ((InvokePattern)invokePattern).Invoke();
                            return true;
                        }
                        break;

                    case "SET_VALUE":
                        if (targetElement.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
                        {
                            ((ValuePattern)valuePattern).SetValue(action.Value);
                            return true;
                        }
                        break;
                }
            }
            catch (Exception) { /* Suppress errors during execution */ }
            return false;
        }

        private void AddElementAndChildren(AutomationElement parent, List<UIElementInfo> elementList)
        {
            if (parent == null) return;

            // Only add elements that are on the screen and potentially interactive
            if (parent.Current.IsOffscreen == false && IsInterestingControlType(parent.Current.ControlType))
            {
                elementList.Add(new UIElementInfo
                {
                    Name = parent.Current.Name,
                    ControlType = parent.Current.ControlType.ProgrammaticName.Split('.').GetValue(1).ToString(),
                    AutomationId = parent.Current.AutomationId,
                    BoundingRectangle = parent.Current.BoundingRectangle
                });
            }

            var children = parent.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement child in children)
            {
                AddElementAndChildren(child, elementList);
            }
        }

        private bool IsInterestingControlType(ControlType controlType)
        {
            return controlType == ControlType.Button ||
                   controlType == ControlType.Edit ||
                   controlType == ControlType.CheckBox ||
                   controlType == ControlType.RadioButton ||
                   controlType == ControlType.ComboBox ||
                   controlType == ControlType.Hyperlink ||
                   controlType == ControlType.ListItem ||
                   controlType == ControlType.TabItem;
        }

        private Condition BuildConditionFromIdentifier(ControlIdentifier identifier)
        {
            var conditions = new List<Condition>();
            if (!string.IsNullOrEmpty(identifier.Name))
            {
                conditions.Add(new PropertyCondition(AutomationElement.NameProperty, identifier.Name));
            }
            if (!string.IsNullOrEmpty(identifier.AutomationId))
            {
                conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, identifier.AutomationId));
            }
            if (!string.IsNullOrEmpty(identifier.Type))
            {
                var controlType = ControlType.LookupById(ControlType.Button.Id); // Default
                if (identifier.Type == "Button") controlType = ControlType.Button;
                if (identifier.Type == "Edit") controlType = ControlType.Edit;
                // ... add more mappings as needed
                conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));
            }

            if (conditions.Count == 0) return Condition.TrueCondition;
            if (conditions.Count == 1) return conditions[0];
            return new AndCondition(conditions.ToArray());
        }
    }
}
