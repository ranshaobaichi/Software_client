using System;
using UnityEngine;

namespace UI.Dialog {
    public interface IDialogMgr {
        int OpenDialog<TDialog>(object input = null, Action<DialogResult> onClose = null, Transform parent = null, bool useBlur = true)
            where TDialog : DialogBase;

        int OpenDialog<TDialog, TInput>(TInput input, Action<DialogResult> onClose = null, Transform parent = null, bool useBlur = true)
            where TDialog : DialogBase<TInput>
            where TInput : class;

        int OpenDialog(Type dialogType, object input = null, Action<DialogResult> onClose = null, Transform parent = null, bool useBlur = true);
    }
}
