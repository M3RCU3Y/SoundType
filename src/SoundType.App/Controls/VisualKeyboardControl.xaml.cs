using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace SoundType.App.Controls;

public partial class VisualKeyboardControl : WpfUserControl
{
    private const double KeyUnitWidth = 34;
    private const double KeyHeight = 38;
    private readonly Dictionary<string, List<WpfButton>> _buttonsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _labelsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<KeyboardKeyToggledEventArgs>? KeyToggled;

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
    }

    private void BuildKeyboard()
    {
        KeyboardGrid.ColumnDefinitions.Clear();
        KeyboardGrid.Children.Clear();
        _buttonsByCode.Clear();
        _labelsByCode.Clear();

        KeyboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        KeyboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        KeyboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddZone(CreateMainRows(), column: 0, rightMargin: 16);
        AddZone(CreateNavigationRows(), column: 1, rightMargin: 16);
        AddZone(CreateNumpadRows(), column: 2, rightMargin: 0);
    }

    private void AddZone(IReadOnlyList<IReadOnlyList<KeyboardKeyDefinition>> rows, int column, double rightMargin)
    {
        Border zone = new()
        {
            Background = (MediaBrush)FindResource("KeyboardZoneBrush"),
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(42, 49, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, rightMargin, 0)
        };

        StackPanel stack = new();
        zone.Child = stack;
        Grid.SetColumn(zone, column);
        KeyboardGrid.Children.Add(zone);

        foreach (IReadOnlyList<KeyboardKeyDefinition> row in rows)
        {
            StackPanel rowPanel = new()
            {
                Orientation = WpfOrientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            foreach (KeyboardKeyDefinition key in row)
            {
                rowPanel.Children.Add(key.IsSpacer ? CreateSpacer(key.Units) : CreateKeyButton(key));
            }

            stack.Children.Add(rowPanel);
        }
    }

    private FrameworkElement CreateSpacer(double units) =>
        new Border
        {
            Width = Math.Max(8, KeyUnitWidth * units),
            Height = KeyHeight,
            Margin = new Thickness(2)
        };

    private WpfButton CreateKeyButton(KeyboardKeyDefinition key)
    {
        string code = key.Code ?? "";
        TextBlock label = new()
        {
            Text = key.Label,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        WpfButton button = new()
        {
            Content = label,
            Tag = code,
            Width = Math.Max(30, KeyUnitWidth * key.Units),
            Height = KeyHeight,
            Style = (Style)FindResource("KeyboardKeyButton"),
            ToolTip = key.ToolTip ?? key.Label
        };
        button.Click += KeyButton_Click;

        if (!_buttonsByCode.TryGetValue(code, out List<WpfButton>? buttons))
        {
            buttons = [];
            _buttonsByCode[code] = buttons;
        }

        buttons.Add(button);
        _labelsByCode[code] = key.ToolTip ?? key.Label;
        UpdateButton(button, _excluded.Contains(code));
        return button;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string code })
        {
            return;
        }

        bool isExcluded = !_excluded.Contains(code);
        SetKeyExcluded(code, isExcluded);
        string displayName = _labelsByCode.TryGetValue(code, out string? label) ? label : code;
        KeyToggled?.Invoke(this, new KeyboardKeyToggledEventArgs(code, displayName, isExcluded));
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
        button.Background = (MediaBrush)FindResource(isExcluded ? "KeyboardKeyExcludedBrush" : "KeyboardKeyBrush");
        button.BorderBrush = (MediaBrush)FindResource(isExcluded ? "KeyboardKeyExcludedBorderBrush" : "KeyboardKeyBorderBrush");
        button.Foreground = isExcluded
            ? (MediaBrush)FindResource("KeyboardKeyExcludedTextBrush")
            : new SolidColorBrush(MediaColor.FromRgb(232, 234, 237));
        button.Opacity = isExcluded ? 0.9 : 1.0;
    }

    private static IReadOnlyList<IReadOnlyList<KeyboardKeyDefinition>> CreateMainRows() =>
    [
        [
            Key("Escape", "Esc"),
            Gap(0.45),
            Key("F1", "F1"), Key("F2", "F2"), Key("F3", "F3"), Key("F4", "F4"),
            Gap(0.45),
            Key("F5", "F5"), Key("F6", "F6"), Key("F7", "F7"), Key("F8", "F8"),
            Gap(0.45),
            Key("F9", "F9"), Key("F10", "F10"), Key("F11", "F11"), Key("F12", "F12")
        ],
        [
            Key("Oem3", "`"),
            Key("D1", "1"), Key("D2", "2"), Key("D3", "3"), Key("D4", "4"), Key("D5", "5"),
            Key("D6", "6"), Key("D7", "7"), Key("D8", "8"), Key("D9", "9"), Key("D0", "0"),
            Key("OemMinus", "-"), Key("OemPlus", "="), Key("Backspace", "Backspace", 2.0)
        ],
        [
            Key("Tab", "Tab", 1.5),
            Key("Q", "Q"), Key("W", "W"), Key("E", "E"), Key("R", "R"), Key("T", "T"),
            Key("Y", "Y"), Key("U", "U"), Key("I", "I"), Key("O", "O"), Key("P", "P"),
            Key("OemOpenBrackets", "["), Key("Oem6", "]"), Key("Oem5", "\\", 1.5)
        ],
        [
            Key("CapsLock", "Caps", 1.75),
            Key("A", "A"), Key("S", "S"), Key("D", "D"), Key("F", "F"), Key("G", "G"),
            Key("H", "H"), Key("J", "J"), Key("K", "K"), Key("L", "L"),
            Key("Oem1", ";"), Key("OemQuotes", "'"), Key("Enter", "Enter", 2.25)
        ],
        [
            Key("LeftShift", "Shift", 2.25),
            Key("Z", "Z"), Key("X", "X"), Key("C", "C"), Key("V", "V"), Key("B", "B"),
            Key("N", "N"), Key("M", "M"), Key("OemComma", ","), Key("OemPeriod", "."),
            Key("OemQuestion", "/"), Key("RightShift", "Shift", 2.75)
        ],
        [
            Key("LeftCtrl", "Ctrl", 1.25),
            Key("LeftWindows", "Win", 1.25),
            Key("LeftAlt", "Alt", 1.25),
            Key("Space", "Space", 6.25),
            Key("RightAlt", "Alt", 1.25),
            Key("RightWindows", "Win", 1.25),
            Key("Apps", "Menu", 1.25),
            Key("RightCtrl", "Ctrl", 1.25)
        ]
    ];

    private static IReadOnlyList<IReadOnlyList<KeyboardKeyDefinition>> CreateNavigationRows() =>
    [
        [Key("PrintScreen", "Prt", toolTip: "Print Screen"), Key("Scroll", "Scr"), Key("Pause", "Pause")],
        [Gap(3.25)],
        [Key("Insert", "Ins"), Key("Home", "Home"), Key("PageUp", "PgUp")],
        [Key("Delete", "Del"), Key("End", "End"), Key("PageDown", "PgDn")],
        [Gap(3.25)],
        [Gap(1.0), Key("Up", "Up"), Gap(1.0)],
        [Key("Left", "Left"), Key("Down", "Down"), Key("Right", "Right")]
    ];

    private static IReadOnlyList<IReadOnlyList<KeyboardKeyDefinition>> CreateNumpadRows() =>
    [
        [Key("NumLock", "Num"), Key("Divide", "/"), Key("Multiply", "*"), Key("Subtract", "-")],
        [Key("NumPad7", "7"), Key("NumPad8", "8"), Key("NumPad9", "9"), Key("Add", "+")],
        [Key("NumPad4", "4"), Key("NumPad5", "5"), Key("NumPad6", "6"), Key("Add", "+")],
        [Key("NumPad1", "1"), Key("NumPad2", "2"), Key("NumPad3", "3"), Key("Enter", "Enter")],
        [Key("NumPad0", "0", 2.0), Key("Decimal", "."), Key("Enter", "Enter")]
    ];

    private static KeyboardKeyDefinition Key(string code, string label, double units = 1.0, string? toolTip = null) =>
        new(code, label, units, toolTip);

    private static KeyboardKeyDefinition Gap(double units) =>
        new(null, "", units, null);

    private sealed record KeyboardKeyDefinition(string? Code, string Label, double Units, string? ToolTip)
    {
        public bool IsSpacer => string.IsNullOrWhiteSpace(Code);
    }
}
