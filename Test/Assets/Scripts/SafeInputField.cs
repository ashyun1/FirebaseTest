using System;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Safe Input Field")]
public class SafeInputField : InputField
{
    protected override void Append(char input)
    {
        NormalizeSelectionForAppend();

        try
        {
            base.Append(input);
        }
        catch (ArgumentOutOfRangeException)
        {
            NormalizeSelectionForAppend(true);

            try
            {
                base.Append(input);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogWarning("InputField caret position was corrected: " + exception.Message);
                UpdateLabel();
            }
        }
    }

    void NormalizeSelectionForAppend(bool forceCollapse = false)
    {
        int maxPosition = string.IsNullOrEmpty(text) ? 0 : text.Length;
        int caretPosition = Mathf.Clamp(caretPositionInternal, 0, maxPosition);
        int selectPosition = Mathf.Clamp(caretSelectPositionInternal, 0, maxPosition);

        bool hasOutOfRangeSelection = caretPosition != caretPositionInternal || selectPosition != caretSelectPositionInternal;
        bool hasImeSelection = !string.IsNullOrEmpty(Input.compositionString) && caretPositionInternal != caretSelectPositionInternal;

        if (!forceCollapse && !hasOutOfRangeSelection && !hasImeSelection)
        {
            return;
        }

        int safePosition = Mathf.Clamp(Mathf.Min(caretPosition, selectPosition), 0, maxPosition);
        caretPositionInternal = safePosition;
        caretSelectPositionInternal = safePosition;
    }
}
