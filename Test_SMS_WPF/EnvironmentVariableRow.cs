using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Test_SMS_WPF;

public sealed class EnvironmentVariableRow : INotifyPropertyChanged
{
    public EnvironmentVariableRow(string field, string comment)
    {
        Field = field;
        Comment = comment;
    }

    public string Field { get; }

    public string Comment { get; }

    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
