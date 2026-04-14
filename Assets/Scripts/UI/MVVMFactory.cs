using UnityEngine;
using UI.ViewModels;
using UI.Views;

namespace UI {
    public static class MVVMFactory {
        public static TVm Bind<TView, TVm>(TView view, TVm vm)
                where TView : ViewBase<TVm>
                where TVm : ViewModelBase<TVm> {
            if (view == null || vm == null) {
                return null;
            }
            view.SetViewModel(vm);
            return vm;
        }

        public static TVm Bind<TView, TVm>(GameObject go, TVm vm)
                where TView : ViewBase<TVm>
                where TVm : ViewModelBase<TVm> {
            if (go == null || vm == null) {
                return null;
            }
            return go.TryGetComponent<TView>(out var view) ? Bind(view, vm) : null;
        }
    }
}
