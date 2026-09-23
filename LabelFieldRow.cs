using System.ComponentModel;
using System.Runtime.CompilerServices;
using BSG.Tools.Models;

namespace BSG.Tools
{
    /// <summary>Editable row of the "Nội dung tem" list in the Settings tab.</summary>
    public class LabelFieldRow : INotifyPropertyChanged
    {
        private bool _enabled;
        private bool _isDragging;
        private bool _isModified;
        private string _label;

        public LabelFieldRow(LabelFieldSetting setting)
        {
            Key = setting.Key;
            _label = setting.Label;
            _enabled = setting.Enabled;
        }

        public string Key { get; }

        // Notifies so unsaved-change tracking can re-run while the label is typed.
        public string Label
        {
            get => _label;
            set
            {
                if (_label == value)
                    return;
                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }

        // True when label, enabled state or position differ from the last saved settings.
        public bool IsModified
        {
            get => _isModified;
            set => Set(ref _isModified, value);
        }

        // Notifies so the row's label TextBox dims as soon as the checkbox is toggled.
        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        // True while this row is being dragged to a new position (dims it).
        public bool IsDragging
        {
            get => _isDragging;
            set => Set(ref _isDragging, value);
        }

        public LabelFieldSetting ToSetting() => new() { Key = Key, Label = Label.Trim(), Enabled = Enabled };

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
