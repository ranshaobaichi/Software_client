namespace UI.SampleScene {
    public class TestInitViewModel {
        public int state1Number;
        
        public TestInitViewModel(int number = -1) {
            state1Number = number;
        }

        public void Render(int number) {
            state1Number = number;
        }
    }
}