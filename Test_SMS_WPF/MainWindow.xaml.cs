using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;

namespace Test_SMS_WPF;

public partial class MainWindow : Window
{
    private const EnvironmentVariableTarget PersistTarget = EnvironmentVariableTarget.User;

    private string? _valueEditOriginal;
    private EnvironmentVariableRow? _valueEditRow;

    public ObservableCollection<EnvironmentVariableRow> Rows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => LoadFromConfiguration();
    }

    private void LoadFromConfiguration()
    {
        Rows.Clear();

        var basePath = AppContext.BaseDirectory;
        var settingsPath = Path.Combine(basePath, "appsettings.json");
        if (!File.Exists(settingsPath))
        {
            MessageBox.Show(
                $"Не найден файл конфигурации:\n{settingsPath}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            EnvChangeLogger.Write($"ERROR: appsettings.json not found at '{settingsPath}'.");
            return;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var names = configuration.GetSection("EnvironmentVariableNames").Get<string[]>()?
            .Where(static n => !string.IsNullOrWhiteSpace(n))
            .Select(static n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (names.Length == 0)
        {
            MessageBox.Show(
                "В appsettings.json не задан массив EnvironmentVariableNames или он пуст.",
                "Конфигурация",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            EnvChangeLogger.Write("WARNING: EnvironmentVariableNames is empty.");
            return;
        }

        var defaults = configuration.GetSection("DefaultValues").GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var comments = configuration.GetSection("Comments").GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var existing = Environment.GetEnvironmentVariable(name, PersistTarget);
            if (existing is null)
            {
                var def = defaults.GetValueOrDefault(name, string.Empty);
                try
                {
                    Environment.SetEnvironmentVariable(name, def, PersistTarget);
                    EnvChangeLogger.Write(
                        $"INIT: variable '{name}' was missing; set user default (length {def.Length}).");
                }
                catch (Exception ex)
                {
                    EnvChangeLogger.Write($"ERROR: failed to initialize '{name}': {ex.Message}");
                    MessageBox.Show(
                        $"Не удалось создать переменную '{name}' в профиле пользователя.\n{ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    continue;
                }
            }

            var value = Environment.GetEnvironmentVariable(name, PersistTarget) ?? string.Empty;
            var commentText = comments.GetValueOrDefault(name, string.Empty);
            var row = new EnvironmentVariableRow(name, commentText);
            row.Value = value;
            Rows.Add(row);
        }

        EnvChangeLogger.Write($"START: loaded {Rows.Count} variable(s) from configuration.");
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void VariablesGrid_OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Column != ValueColumn || e.Row.Item is not EnvironmentVariableRow row)
            return;

        _valueEditRow = row;
        _valueEditOriginal = Environment.GetEnvironmentVariable(row.Field, PersistTarget) ?? string.Empty;
    }

    private void VariablesGrid_OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.Column != ValueColumn || e.EditingElement is not TextBox tb)
            return;

        tb.AcceptsReturn = true;
        tb.TextWrapping = TextWrapping.Wrap;
        tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private void VariablesGrid_OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column != ValueColumn || e.Row.Item is not EnvironmentVariableRow row)
            return;

        var newValue = e.EditingElement is TextBox tb ? tb.Text : row.Value;
        var oldValue = _valueEditRow == row && _valueEditOriginal is not null
            ? _valueEditOriginal
            : Environment.GetEnvironmentVariable(row.Field, PersistTarget) ?? string.Empty;

        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            _valueEditRow = null;
            _valueEditOriginal = null;
            return;
        }

        try
        {
            Environment.SetEnvironmentVariable(row.Field, newValue, PersistTarget);
            row.Value = newValue;
            EnvChangeLogger.Write(
                $"CHANGE: '{row.Field}' updated (old length {oldValue.Length}, new length {newValue.Length}).");
        }
        catch (Exception ex)
        {
            e.Cancel = true;
            EnvChangeLogger.Write($"ERROR: failed to set '{row.Field}': {ex.Message}");
            MessageBox.Show(
                $"Не удалось записать переменную '{row.Field}'.\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            row.Value = oldValue;
        }

        _valueEditRow = null;
        _valueEditOriginal = null;
    }
}
