// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements
{
    public abstract class BaseCompositeField<TValueType, TField, TFieldValue> : BaseField<TValueType>
        where TField : TextValueField<TFieldValue>, new()
    {
        internal struct FieldDescription
        {
            public delegate void WriteDelegate(ref TValueType val, TFieldValue fieldValue);

            internal readonly string name;
            internal readonly string ussName;
            internal readonly Func<TValueType, TFieldValue> read;
            internal readonly WriteDelegate write;

            public FieldDescription(string name, string ussName, Func<TValueType, TFieldValue> read, WriteDelegate write)
            {
                this.name = name;
                this.ussName = ussName;
                this.read = read;
                this.write = write;
            }
        }

        public new class UxmlTraits : BaseField<TValueType>.UxmlTraits
        {
            UxmlStringAttributeDescription m_Label = new UxmlStringAttributeDescription { name = "label" };

            protected UxmlTraits()
            {
                focusIndex.defaultValue = 0;
                focusable.defaultValue = true;
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((BaseField<TValueType>)ve).label = m_Label.GetValueFromBag(bag, cc);
            }
        }

        public override bool focusable
        {
            get { return base.focusable; }
            set
            {
                base.focusable = value;
                if ((m_Fields != null) && (m_Fields.Count > 0))
                {
                    foreach (var field in m_Fields)
                    {
                        field.focusable = focusable;
                    }
                }
            }
        }

        public override int tabIndex
        {
            get { return base.tabIndex; }
            set
            {
                base.tabIndex = value;
                if ((m_Fields != null) && (m_Fields.Count > 0))
                {
                    foreach (var field in m_Fields)
                    {
                        field.tabIndex = value;
                    }
                }
            }
        }

        private VisualElement GetSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList(spacerUssClassName);
            spacer.visible = false;
            spacer.focusable = false;
            return spacer;
        }

        List<TField> m_Fields;
        internal List<TField> fields => m_Fields;

        internal abstract FieldDescription[] DescribeFields();
        bool m_ShouldUpdateDisplay;

        public new static readonly string ussClassName = "unity-composite-field";
        public static readonly string spacerUssClassName = ussClassName + "__field-spacer";
        public static readonly string multilineVariantUssClassName = ussClassName + "--multi-line";
        public static readonly string fieldGroupUssClassName = ussClassName + "__field-group";
        public static readonly string fieldUssClassName = ussClassName + "__field";
        public static readonly string firstFieldVariantUssClassName = fieldUssClassName + "--first";
        public static readonly string twoLinesVariantUssClassName = ussClassName + "--two-lines";

        protected BaseCompositeField(string label, int fieldsByLine)
            : base(label, null)
        {
            AddToClassList(ussClassName);
            m_ShouldUpdateDisplay = true;
            m_Fields = new List<TField>();
            FieldDescription[] fieldDescriptions = DescribeFields();

            int numberOfLines = 1;
            if (fieldsByLine > 1)
            {
                numberOfLines = fieldDescriptions.Length / fieldsByLine;
            }

            var isMultiLine = false;
            if (numberOfLines > 1)
            {
                isMultiLine = true;
                AddToClassList(multilineVariantUssClassName);
            }

            for (int i = 0; i < numberOfLines; i++)
            {
                VisualElement newLineGroup = null;
                if (isMultiLine)
                {
                    newLineGroup = new VisualElement();
                    newLineGroup.AddToClassList(fieldGroupUssClassName);
                }

                bool firstField = true;
                for (int j = i * fieldsByLine; j < ((i * fieldsByLine) + fieldsByLine); j++)
                {
                    var desc = fieldDescriptions[j];
                    var field = new TField()
                    {
                        name = desc.ussName
                    };
                    field.AddToClassList(fieldUssClassName);
                    if (firstField)
                    {
                        field.AddToClassList(firstFieldVariantUssClassName);
                        firstField = false;
                    }

                    field.label = desc.name;
                    field.RegisterValueChangedCallback(e =>
                    {
                        TValueType cur = value;
                        desc.write(ref cur, e.newValue);

                        // Here, just check and make sure the text is updated in the basic field and is the same as the value...
                        // For example, backspace done on a selected value will empty the field (text == "") but the value will be 0.
                        // Or : a text of "2+3" is valid until enter is pressed, so not equal to a value of "5".
                        if (e.newValue.ToString() != ((TField)e.currentTarget).text)
                        {
                            m_ShouldUpdateDisplay = false;
                        }

                        value = cur;
                        m_ShouldUpdateDisplay = true;
                    });
                    m_Fields.Add(field);
                    if (isMultiLine)
                    {
                        newLineGroup.Add(field);
                    }
                    else
                    {
                        visualInput.hierarchy.Add(field);
                    }
                }

                if (fieldsByLine < 3)
                {
                    int fieldsToAdd = 3 - fieldsByLine;
                    for (int countToAdd = 0; countToAdd < fieldsToAdd; countToAdd++)
                    {
                        if (isMultiLine)
                        {
                            newLineGroup.Add(GetSpacer());
                        }
                        else
                        {
                            visualInput.hierarchy.Add(GetSpacer());
                        }
                    }
                }

                if (isMultiLine)
                {
                    visualInput.hierarchy.Add(newLineGroup);
                }
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (m_Fields.Count != 0)
            {
                var i = 0;
                FieldDescription[] fieldDescriptions = DescribeFields();
                foreach (var fd in fieldDescriptions)
                {
                    m_Fields[i].value = (fd.read(rawValue));
                    i++;
                }
            }
        }

        public override void SetValueWithoutNotify(TValueType newValue)
        {
            var displayNeedsUpdate = m_ShouldUpdateDisplay && !EqualityComparer<TValueType>.Default.Equals(rawValue, newValue);

            // Make sure to call the base class to set the value...
            base.SetValueWithoutNotify(newValue);

            // Before Updating the display, just check if the value changed...
            if (displayNeedsUpdate)
            {
                UpdateDisplay();
            }
        }

        protected internal override void ExecuteDefaultAction(EventBase evt)
        {
            base.ExecuteDefaultAction(evt);

            // Focus first field if any
            if (evt?.eventTypeId == FocusEvent.TypeId() && m_Fields.Count > 0)
                m_Fields[0].Focus();
        }
    }
}