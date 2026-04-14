using UI.ViewModels;

namespace UI.SampleScene {
    public class TestInitViewModel : ViewModelBase<TestInitViewModel> {
        public int state1Number;

        public TestInitViewModel(int number = -1) {
            state1Number = number;
        }

        public void SetState1Number(int number) {
            if (state1Number == number) {
                return;
            }
            state1Number = number;
            RaisePropertyChanged(nameof(state1Number));
        }
    }
}
