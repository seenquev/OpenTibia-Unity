﻿using UnityEngine;

namespace OpenTibiaUnity.Modules.Skills
{
    public class SkillRawPanel : SkillPanel
    {
        [SerializeField] private TMPro.TextMeshProUGUI _labelText = null;
        [SerializeField] private TMPro.TextMeshProUGUI _labelValue = null;

        public override TMPro.TextMeshProUGUI labelText { get => _labelText; }
        public override TMPro.TextMeshProUGUI labelValue { get => _labelValue; }

        public override void SetText(string text) => _labelText.SetText(text);
        public override void SetValue(long value) => SetValuepublic(Core.Utils.Utility.Commafy(value));

        public void SetValuepublic(string value) => _labelValue.SetText(value);
    }
}
