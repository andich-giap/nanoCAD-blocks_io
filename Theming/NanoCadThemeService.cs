using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;

namespace NanoCadIo.Theming
{
	public sealed class NanoCadThemeService
	{
		private static readonly Lazy<NanoCadThemeService> _instance =
			new(() => new NanoCadThemeService());

		public static NanoCadThemeService Instance => _instance.Value;

		private NanoCadThemeService()
		{
			CurrentTheme = DetectTheme();
		}

		public NanoCadTheme CurrentTheme { get; private set; }

		public event EventHandler<NanoCadTheme>? ThemeChanged;

		public NanoCadTheme DetectTheme()
		{
			// 1) nanoCAD sysvar (AutoCAD-compatible): 0 = dark, 1 = light
			try
			{
				object value = HostMgd.ApplicationServices.Application.GetSystemVariable("COLORTHEME");
				if (value is short s) return s == 0 ? NanoCadTheme.Dark : NanoCadTheme.Light;
				if (value is int i) return i == 0 ? NanoCadTheme.Dark : NanoCadTheme.Light;
			}
			catch
			{
				// sysvar may not exist in this nanoCAD version — fall through
			}

			// 2) Registry
			try
			{
				var fromReg = ReadThemeFromRegistry();
				if (fromReg.HasValue) return fromReg.Value;
			}
			catch
			{
				// ignore and fall through
			}

			return NanoCadTheme.Dark;
		}

		private static NanoCadTheme? ReadThemeFromRegistry()
		{
			string[] candidates =
			{
				@"Software\Nanosoft\nanoCAD\Appearance",
				@"Software\Nanosoft\nanoCAD\24.0\Appearance",
				@"Software\Nanosoft\nanoCAD\23.0\Appearance",
				@"Software\Nanosoft\nanoCAD\22.0\Appearance",
				@"Software\Nanosoft\nanoCAD\21.0\Appearance"
			};

			string[] valueNames = { "ColorTheme", "Theme", "AppTheme" };

			foreach (var path in candidates)
			{
				using var key = Registry.CurrentUser.OpenSubKey(path);
				if (key == null) continue;

				foreach (var name in valueNames)
				{
					if (key.GetValue(name) is string s)
					{
						if (s.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return NanoCadTheme.Dark;
						if (s.Equals("Light", StringComparison.OrdinalIgnoreCase)) return NanoCadTheme.Light;
					}
					if (key.GetValue(name) is int n)
						return n == 0 ? NanoCadTheme.Dark : NanoCadTheme.Light;
				}
			}

			return null;
		}

		public void Refresh(FrameworkElement root)
		{
			var detected = DetectTheme();
			CurrentTheme = detected;
			Apply(root);
			ThemeChanged?.Invoke(this, detected);
		}

		public void Apply(FrameworkElement root)
		{
			var dict = BuildResourceDictionary(CurrentTheme);
			var merged = root.Resources.MergedDictionaries;

			var old = merged.FirstOrDefault(d => d.Contains("Theme.WindowBackground"));
			if (old != null)
				merged.Remove(old);

			merged.Add(dict);
		}

		public static ResourceDictionary BuildResourceDictionary(NanoCadTheme theme)
		{
			var p = ThemePalette.For(theme);

			return new ResourceDictionary
			{
				["Theme.WindowBackground"]  = Freeze(new SolidColorBrush(p.WindowBackground)),
				["Theme.ControlBackground"] = Freeze(new SolidColorBrush(p.ControlBackground)),
				["Theme.Foreground"]        = Freeze(new SolidColorBrush(p.Foreground)),
				["Theme.Border"]            = Freeze(new SolidColorBrush(p.Border)),
				["Theme.Accent"]            = Freeze(new SolidColorBrush(p.Accent)),
				["Theme.GrayText"]          = Freeze(new SolidColorBrush(p.GrayText)),
				["Theme.Error"]             = Freeze(new SolidColorBrush(p.Error)),
				["Theme.ListBackground"]    = Freeze(new SolidColorBrush(p.ListBackground)),
				["Theme.ListItemHover"]     = Freeze(new SolidColorBrush(p.ListItemHover)),
				["Theme.ListItemSelected"]  = Freeze(new SolidColorBrush(p.ListItemSelected)),
				["Theme.ButtonBackground"]  = Freeze(new SolidColorBrush(p.ButtonBackground)),
				["Theme.ButtonHover"]       = Freeze(new SolidColorBrush(p.ButtonHover)),
				["Theme.ButtonPressed"]     = Freeze(new SolidColorBrush(p.ButtonPressed))
			};
		}

		private static Brush Freeze(SolidColorBrush brush)
		{
			brush.Freeze();
			return brush;
		}
	}
}
