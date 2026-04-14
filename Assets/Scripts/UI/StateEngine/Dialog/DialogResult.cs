namespace UI.Dialog {
    public struct DialogResult {
        public static readonly DialogResult Empty = default;

        public int intVal;
        public string strVal;
        public object objVal;

        public static implicit operator DialogResult(bool value) =>
            new DialogResult { intVal = value ? 1 : 0 };

        public static implicit operator DialogResult(int value) =>
            new DialogResult { intVal = value };

        public static implicit operator DialogResult(string value) =>
            new DialogResult { strVal = value };
        
        public static DialogResult FromObject(object value) {
            if (value == null) {
                return Empty;
            }

            if (value is bool b) {
                return b;
            }

            if (value is int i) {
                return i;
            }

            if (value is string s) {
                return s;
            }

            return new DialogResult { objVal = value };
        }
    }
}
