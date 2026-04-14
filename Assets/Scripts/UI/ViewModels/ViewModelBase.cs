using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI.ViewModels {
    /// <summary>
    /// 非泛型根：供 View 统一订阅 <see cref="INotifyPropertyChanged"/>。
    /// 可绑定数据在子类中变更后手动调用 <see cref="RaisePropertyChanged"/>。
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// CRTP：具体 VM 声明为 <c>class FooVm : ViewModelBase&lt;FooVm&gt;</c>。
    /// </summary>
    public abstract class ViewModelBase<TSelf> : ViewModelBase where TSelf : ViewModelBase<TSelf> {
        protected TSelf self => (TSelf)(object)this;
    }
}