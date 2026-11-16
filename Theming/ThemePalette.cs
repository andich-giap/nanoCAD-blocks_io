using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace NanoCadIo.Theming
{
    public enum NanoCadTheme { Dark, Light }

    public sealed class ThemePalette
    {
        public Color WindowBackground { get; init; }
        public Color ControlBackground { get; init; }
        public Color Foreground { get; init; }
        public Color Border { get; init; }
        public Color Accent { get; init; }
        public Color GrayText { get; init; }
        public Color Error { get; init; }
        public Color ListBackground { get; init; }
        public Color ListItemHover { get; init; }
        public Color ListItemSelected { get; init; }
        public Color ButtonBackground { get; init; }
        public Color ButtonHover { get; init; }
        public Color ButtonPressed { get; init; }

        // Подобрано по скриншоту нативной панели nanoCAD 24 (тёмная тема)
        public static readonly ThemePalette Dark = new()
        {
            WindowBackground   = Color.FromRgb(0x3C, 0x3C, 0x3C), // как у «Свойства»
            ControlBackground  = Color.FromRgb(0x42, 0x42, 0x42), // поля/чекбоксы чуть светлее
            Foreground         = Color.FromRgb(0xD5, 0xD5, 0xD5), // мягкий белый
            Border             = Color.FromRgb(0x4A, 0x4A, 0x4A), // тонкие рамки
            Accent             = Color.FromRgb(0x00, 0x78, 0xD4), // Win11 синий
            GrayText           = Color.FromRgb(0x99, 0x99, 0x99),
            Error              = Color.FromRgb(0xFF, 0x6B, 0x6B),
            ListBackground     = Color.FromRgb(0x3C, 0x3C, 0x3C), // = фон панели!
            ListItemHover      = Color.FromRgb(0x46, 0x46, 0x46), // едва заметный hover
            ListItemSelected   = Color.FromRgb(0x2D, 0x5B, 0x8B), // как в «Выбор»
            ButtonBackground   = Color.FromRgb(0x42, 0x42, 0x42), // «Применить»
            ButtonHover        = Color.FromRgb(0x4D, 0x4D, 0x4D),
            ButtonPressed      = Color.FromRgb(0x55, 0x55, 0x55)
        };

        public static readonly ThemePalette Light = new()
        {
            WindowBackground   = Color.FromRgb(0xF0, 0xF0, 0xF0),
            ControlBackground  = Color.FromRgb(0xFF, 0xFF, 0xFF),
            Foreground         = Color.FromRgb(0x1A, 0x1A, 0x1A),
            Border             = Color.FromRgb(0xCC, 0xCC, 0xCC),
            Accent             = Color.FromRgb(0x00, 0x78, 0xD4),
            GrayText           = Color.FromRgb(0x66, 0x66, 0x66),
            Error              = Color.FromRgb(0xC4, 0x2B, 0x1C),
            ListBackground     = Color.FromRgb(0xFF, 0xFF, 0xFF),
            ListItemHover      = Color.FromRgb(0xE5, 0xF3, 0xFF),
            ListItemSelected   = Color.FromRgb(0xCC, 0xE4, 0xF7),
            ButtonBackground   = Color.FromRgb(0xE1, 0xE1, 0xE1),
            ButtonHover        = Color.FromRgb(0xD5, 0xD5, 0xD5),
            ButtonPressed      = Color.FromRgb(0xC0, 0xC0, 0xC0)
        };

        public static ThemePalette For(NanoCadTheme theme) =>
            theme == NanoCadTheme.Dark ? Dark : Light;
    }
}
