using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace SoundType.App.Controls;

public partial class VisualKeyboardControl : WpfUserControl
{
    private const double KeyWidth = 33;
    private const double KeyHeight = 54;
    private const double KeyGap = 5;
    private const double MainFunctionGap = 26;
    private const double ZoneGap = 18;
    private readonly Dictionary<string, List<WpfButton>> _buttonsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _labelsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedCode = "Space";
    private string _searchText = "";
    private KeyboardKeyFilter _filter = KeyboardKeyFilter.All;

    public event EventHandler<KeyboardKeyToggledEventArgs>? KeyToggled;
    public event EventHandler<KeyboardKeySelectedEventArgs>? KeySelected;

    public int KeyCount => _buttonsByCode.Keys.Count;
    public int ExcludedCount => _excluded.Count(code => _buttonsByCode.ContainsKey(code));
    public int EnabledCount => Math.Max(0, KeyCount - ExcludedCount);

    public VisualKeyboardControl()
    {
        InitializeComponent();
        BuildKeyboard();
    }

    public void SetExcludedKeys(IEnumerable<string> excludedCodes)
    {
        _excluded.Clear();
        foreach (string code in excludedCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
        {
            _excluded.Add(code);
        }

        foreach (string code in _buttonsByCode.Keys)
        {
            UpdateKeyState(code);
        }

        ApplyFilter();
    }

    public void SetKeyExcluded(string code, bool isExcluded)
    {
        if (isExcluded)
        {
            _excluded.Add(code);
        }
        else
        {
            _excluded.Remove(code);
        }

        UpdateKeyState(code);
        ApplyFilter();
    }

    public bool IsKeyExcluded(string code) => _excluded.Contains(code);

    public void SelectKey(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !_buttonsByCode.ContainsKey(code))
        {
            return;
        }

        string? previousCode = _selectedCode;
        _selectedCode = code;
        if (!string.IsNullOrWhiteSpace(previousCode))
        {
            UpdateKeyState(previousCode);
        }

        UpdateKeyState(code);
        RaiseKeySelected(code);
    }

    public void ApplyFilter(string searchText, KeyboardKeyFilter filter)
    {
        _searchText = searchText.Trim();
        _filter = filter;
        ApplyFilter();
    }

    private void BuildKeyboard()
    {
        KeyboardCanvas.Children.Clear();
        _buttonsByCode.Clear();
        _labelsByCode.Clear();

        double y0 = 0;
        double y1 = 82;
        double y2 = y1 + KeyHeight + 7;
        double y3 = y2 + KeyHeight + 7;
        double y4 = y3 + KeyHeight + 7;
        double y5 = y4 + KeyHeight + 7;

        double mainX = 0;
        AddMainFunctionRow(mainX, y0);
        AddMainRow(mainX, y1, Row1());
        AddMainRow(mainX, y2, Row2());
        AddMainRow(mainX, y3, Row3());
        AddMainRow(mainX, y4, Row4());
        AddMainRow(mainX, y5, Row5());

        double navX = mainX + MeasureMainRow(Row1()) + ZoneGap;
        AddNavigationKeys(navX, y0, y1, y2, y3, y4, y5);

        double numpadX = navX + (3 * KeyWidth) + (2 * KeyGap) + ZoneGap;
        AddNumpadKeys(numpadX, y1, y2, y3, y4, y5);

        KeyboardCanvas.Width = numpadX + (4 * KeyWidth) + (3 * KeyGap);
        KeyboardCanvas.Height = y5 + KeyHeight;
    }

    private void AddMainFunctionRow(double x, double y)
    {
        AddKey(Key("Escape", "Esc"), x, y);
        x += KeyWidth + MainFunctionGap + 10;
        foreach (KeyboardKeyDefinition key in new[]
                 {
                     Key("F1", "F1"), Key("F2", "F2"), Key("F3", "F3"), Key("F4", "F4")
                 })
        {
            AddKey(key, x, y);
            x += KeyWidth + KeyGap;
        }

        x += MainFunctionGap - KeyGap + 2;
        foreach (KeyboardKeyDefinition key in new[]
                 {
                     Key("F5", "F5"), Key("F6", "F6"), Key("F7", "F7"), Key("F8", "F8")
                 })
        {
            AddKey(key, x, y);
            x += KeyWidth + KeyGap;
        }

        x += MainFunctionGap - KeyGap + 2;
        foreach (KeyboardKeyDefinition key in new[]
                 {
                     Key("F9", "F9"), Key("F10", "F10"), Key("F11", "F11"), Key("F12", "F12")
                 })
        {
            AddKey(key, x, y);
            x += KeyWidth + KeyGap;
        }
    }

    private void AddMainRow(double x, double y, IReadOnlyList<KeyboardKeyDefinition> keys)
    {
        foreach (KeyboardKeyDefinition key in keys)
        {
            AddKey(key, x, y);
            x += KeyWidthFor(key.Units) + KeyGap;
        }
    }

    private void AddNavigationKeys(double x, double y0, double y1, double y2, double y3, double y4, double y5)
    {
        AddKey(Key("PrintScreen", "PrtSc", toolTip: "Print Screen"), x, y0);
        AddKey(Key("Scroll", "ScrLk", toolTip: "Scroll Lock"), x + KeyWidth + KeyGap, y0);
        AddKey(Key("Pause", "Pause"), x + 2 * (KeyWidth + KeyGap), y0);

        AddKey(Key("Insert", "Ins"), x, y1);
        AddKey(Key("Home", "Home"), x + KeyWidth + KeyGap, y1);
        AddKey(Key("PageUp", "PgUp"), x + 2 * (KeyWidth + KeyGap), y1);

        AddKey(Key("Delete", "Del"), x, y2);
        AddKey(Key("End", "End"), x + KeyWidth + KeyGap, y2);
        AddKey(Key("PageDown", "PgDn"), x + 2 * (KeyWidth + KeyGap), y2);

        AddKey(Key("Up", "\u2191", toolTip: "Up"), x + KeyWidth + KeyGap, y4);
        AddKey(Key("Left", "\u2190", toolTip: "Left"), x, y5);
        AddKey(Key("Down", "\u2193", toolTip: "Down"), x + KeyWidth + KeyGap, y5);
        AddKey(Key("Right", "\u2192", toolTip: "Right"), x + 2 * (KeyWidth + KeyGap), y5);
    }

    private void AddNumpadKeys(double x, double y1, double y2, double y3, double y4, double y5)
    {
        AddKey(Key("NumLock", "Num"), x, y1);
        AddKey(Key("Divide", "/"), x + KeyWidth + KeyGap, y1);
        AddKey(Key("Multiply", "*"), x + 2 * (KeyWidth + KeyGap), y1);
        AddKey(Key("Subtract", "-"), x + 3 * (KeyWidth + KeyGap), y1);

        AddKey(Key("NumPad7", "7"), x, y2);
        AddKey(Key("NumPad8", "8"), x + KeyWidth + KeyGap, y2);
        AddKey(Key("NumPad9", "9"), x + 2 * (KeyWidth + KeyGap), y2);
        AddKey(Key("Add", "+", rowSpan: 2), x + 3 * (KeyWidth + KeyGap), y2);

        AddKey(Key("NumPad4", "4"), x, y3);
        AddKey(Key("NumPad5", "5"), x + KeyWidth + KeyGap, y3);
        AddKey(Key("NumPad6", "6"), x + 2 * (KeyWidth + KeyGap), y3);

        AddKey(Key("NumPad1", "1"), x, y4);
        AddKey(Key("NumPad2", "2"), x + KeyWidth + KeyGap, y4);
        AddKey(Key("NumPad3", "3"), x + 2 * (KeyWidth + KeyGap), y4);
        AddKey(Key("Enter", "Enter", rowSpan: 2), x + 3 * (KeyWidth + KeyGap), y4);

        AddKey(Key("NumPad0", "0", 2), x, y5);
        AddKey(Key("Decimal", "."), x + KeyWidthFor(2) + KeyGap, y5);
    }

    private void AddKey(KeyboardKeyDefinition key, double x, double y)
    {
        WpfButton button = CreateKeyButton(key);
        Canvas.SetLeft(button, x);
        Canvas.SetTop(button, y);
        KeyboardCanvas.Children.Add(button);
    }

    private WpfButton CreateKeyButton(KeyboardKeyDefinition key)
    {
        string code = key.Code;
        TextBlock label = new()
        {
            Text = key.Label,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineHeight = 15
        };
        if (key.Label.Equals("\uE782", StringComparison.Ordinal))
        {
            label.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
        }

        WpfButton button = new()
        {
            Content = label,
            Tag = code,
            Width = KeyWidthFor(key.Units),
            Height = KeyHeightFor(key.RowSpan),
            Style = (Style)FindResource("KeyboardKeyButton"),
            ToolTip = key.ToolTip ?? key.PlainLabel
        };
        button.Click += KeyButton_Click;

        if (!_buttonsByCode.TryGetValue(code, out List<WpfButton>? buttons))
        {
            buttons = [];
            _buttonsByCode[code] = buttons;
        }

        buttons.Add(button);
        _labelsByCode[code] = key.ToolTip ?? key.PlainLabel;
        UpdateButton(button, _excluded.Contains(code));
        return button;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string code })
        {
            return;
        }

        string? previousCode = _selectedCode;
        _selectedCode = code;
        if (!string.IsNullOrWhiteSpace(previousCode) && !previousCode.Equals(code, StringComparison.OrdinalIgnoreCase))
        {
            UpdateKeyState(previousCode);
        }

        bool isExcluded = !_excluded.Contains(code);
        SetKeyExcluded(code, isExcluded);
        string displayName = _labelsByCode.TryGetValue(code, out string? label) ? label : code;
        KeyToggled?.Invoke(this, new KeyboardKeyToggledEventArgs(code, displayName, isExcluded));
        KeySelected?.Invoke(this, new KeyboardKeySelectedEventArgs(code, displayName, isExcluded));
    }

    private void UpdateKeyState(string code)
    {
        if (!_buttonsByCode.TryGetValue(code, out List<WpfButton>? buttons))
        {
            return;
        }

        bool isExcluded = _excluded.Contains(code);
        foreach (WpfButton button in buttons)
        {
            UpdateButton(button, isExcluded);
        }
    }

    private void UpdateButton(WpfButton button, bool isExcluded)
    {
        bool isSelected = button.Tag is string code &&
            code.Equals(_selectedCode, StringComparison.OrdinalIgnoreCase);

        button.Background = isSelected
            ? new SolidColorBrush(MediaColor.FromRgb(21, 69, 51))
            : (MediaBrush)FindResource(isExcluded ? "KeyboardKeyExcludedBrush" : "KeyboardKeyBrush");
        button.BorderBrush = isSelected
            ? (MediaBrush)FindResource("AccentHoverBrush")
            : (MediaBrush)FindResource(isExcluded ? "KeyboardKeyExcludedBorderBrush" : "KeyboardKeyBorderBrush");
        button.Foreground = isExcluded
            ? (MediaBrush)FindResource("KeyboardKeyExcludedTextBrush")
            : new SolidColorBrush(MediaColor.FromRgb(246, 248, 250));
        button.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        button.Opacity = isExcluded ? 0.96 : 1.0;
    }

    private void ApplyFilter()
    {
        foreach ((string code, List<WpfButton> buttons) in _buttonsByCode)
        {
            bool isExcluded = _excluded.Contains(code);
            bool matchesFilter = _filter switch
            {
                KeyboardKeyFilter.Enabled => !isExcluded,
                KeyboardKeyFilter.Excluded => isExcluded,
                _ => true
            };
            bool matchesSearch = string.IsNullOrWhiteSpace(_searchText) ||
                code.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (_labelsByCode.TryGetValue(code, out string? label) &&
                 label.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            foreach (WpfButton button in buttons)
            {
                button.Opacity = matchesFilter && matchesSearch
                    ? (isExcluded ? 0.96 : 1.0)
                    : 0.28;
            }
        }
    }

    private void RaiseKeySelected(string code)
    {
        string displayName = _labelsByCode.TryGetValue(code, out string? label) ? label : code;
        KeySelected?.Invoke(this, new KeyboardKeySelectedEventArgs(code, displayName, _excluded.Contains(code)));
    }

    private static double MeasureMainRow(IReadOnlyList<KeyboardKeyDefinition> keys) =>
        keys.Sum(key => KeyWidthFor(key.Units)) + ((keys.Count - 1) * KeyGap);

    private static double KeyWidthFor(double units) =>
        (units * KeyWidth) + ((units - 1) * KeyGap);

    private static double KeyHeightFor(int rowSpan) =>
        (rowSpan * KeyHeight) + ((rowSpan - 1) * 7);

    private static IReadOnlyList<KeyboardKeyDefinition> Row1() =>
    [
        Key("Oem3", "~\n`", toolTip: "`"),
        Key("D1", "!\n1", toolTip: "1"),
        Key("D2", "@\n2", toolTip: "2"),
        Key("D3", "#\n3", toolTip: "3"),
        Key("D4", "$\n4", toolTip: "4"),
        Key("D5", "%\n5", toolTip: "5"),
        Key("D6", "^\n6", toolTip: "6"),
        Key("D7", "&\n7", toolTip: "7"),
        Key("D8", "*\n8", toolTip: "8"),
        Key("D9", "(\n9", toolTip: "9"),
        Key("D0", ")\n0", toolTip: "0"),
        Key("OemMinus", "_\n-", toolTip: "-"),
        Key("OemPlus", "+\n=", toolTip: "="),
        Key("Backspace", "Backspace", 2)
    ];

    private static IReadOnlyList<KeyboardKeyDefinition> Row2() =>
    [
        Key("Tab", "Tab", 1.5),
        Key("Q", "Q"), Key("W", "W"), Key("E", "E"), Key("R", "R"), Key("T", "T"),
        Key("Y", "Y"), Key("U", "U"), Key("I", "I"), Key("O", "O"), Key("P", "P"),
        Key("OemOpenBrackets", "{\n[", toolTip: "["),
        Key("Oem6", "}\n]", toolTip: "]"),
        Key("Oem5", "|\n\\", 1.5, toolTip: "\\")
    ];

    private static IReadOnlyList<KeyboardKeyDefinition> Row3() =>
    [
        Key("CapsLock", "CapsLock", 1.75),
        Key("A", "A"), Key("S", "S"), Key("D", "D"), Key("F", "F"), Key("G", "G"),
        Key("H", "H"), Key("J", "J"), Key("K", "K"), Key("L", "L"),
        Key("Oem1", ":\n;", toolTip: ";"),
        Key("OemQuotes", "\"\n'", toolTip: "'"),
        Key("Enter", "Enter", 2.25)
    ];

    private static IReadOnlyList<KeyboardKeyDefinition> Row4() =>
    [
        Key("LeftShift", "Shift", 2.25),
        Key("Z", "Z"), Key("X", "X"), Key("C", "C"), Key("V", "V"), Key("B", "B"),
        Key("N", "N"), Key("M", "M"), Key("OemComma", "<\n,", toolTip: ","),
        Key("OemPeriod", ">\n.", toolTip: "."),
        Key("OemQuestion", "?\n/", toolTip: "/"),
        Key("RightShift", "Shift", 2.75)
    ];

    private static IReadOnlyList<KeyboardKeyDefinition> Row5() =>
    [
        Key("LeftCtrl", "Ctrl", 1.35),
        Key("LeftWindows", "\uE782", 1.35, toolTip: "Windows"),
        Key("LeftAlt", "Alt", 1.35),
        Key("Space", "Space", 6.05),
        Key("RightAlt", "Alt", 1.25),
        Key("RightWindows", "\uE782", 1.25, toolTip: "Windows"),
        Key("Apps", "Menu", 1.25),
        Key("RightCtrl", "Ctrl", 1.25)
    ];

    private static KeyboardKeyDefinition Key(
        string code,
        string label,
        double units = 1.0,
        string? toolTip = null,
        int rowSpan = 1) =>
        new(code, label, units, toolTip, rowSpan);

    private sealed record KeyboardKeyDefinition(
        string Code,
        string Label,
        double Units,
        string? ToolTip,
        int RowSpan)
    {
        public string PlainLabel => Label.Replace("\n", "", StringComparison.Ordinal);
    }
}

public enum KeyboardKeyFilter
{
    All,
    Enabled,
    Excluded
}
