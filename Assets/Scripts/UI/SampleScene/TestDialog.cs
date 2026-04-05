using UnityEngine;
using UnityEngine.UI;
using UI.Dialog;

namespace UI.SampleScene {
    public class TestDialogInputData {
        public int initIndex;
        public string strVal;
    }
    
    public class TestDialog : DialogBase<TestDialogInputData> {
        [SerializeField]
        private Text _strText;
        
        private int m_selectedIndex;
        
        public void SelectIndex(int index) {
            m_selectedIndex = index;
            Close(m_selectedIndex);
        }
        
        protected override void OnRender(TestDialogInputData input) {
            _strText.text = input.strVal;
            m_selectedIndex = input.initIndex;
        }

        public void OnClose() {
            Close(m_selectedIndex);
        }
    }
}