using UnityEngine;
using System;
using UI.Dialog;
using UI.Page;

namespace UI.Pages {
    public class CommonPage : PageBase, IDialogMgr {
        [SerializeField]
        private DialogRegistry _dialogRegistry;

        private DialogMgr m_dialogMgr;

        protected override void OnInitialize(Camera ca) {
            m_dialogMgr = DialogMgr.Create(new DialogMgr.Builder {
                    registry = _dialogRegistry,
                    screenshotCameras = new[] { ca },
            });
        }
        
        public int OpenDialog<TDialog>(object input = null,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        )
                where TDialog : DialogBase {
            parent ??= transform;
            return m_dialogMgr.OpenDialog<TDialog>(input, onClose, parent, useBlur);
        }

        public int OpenDialog<TDialog, TInput>(TInput input,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        )
                where TDialog : DialogBase<TInput>
                where TInput : class {
            parent ??= transform;
            return m_dialogMgr.OpenDialog<TDialog, TInput>(input, onClose, parent, useBlur);
        }

        public int OpenDialog(Type dialogType,
                object input = null,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        ) {
            parent ??= transform;
            return m_dialogMgr.OpenDialog(dialogType, input, onClose, parent, useBlur);
        }
    }
}