using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace XO_Launcher.Services
{
    /// <summary>启动器亮色 / 暗色主题。由设置页个性化板块切换，作用到主窗口及对话框。</summary>
    public static class ThemeService
    {
        public static bool IsLight =>
            string.Equals(LauncherSettings.Instance.AppTheme, "light", System.StringComparison.OrdinalIgnoreCase);

        public static ElementTheme Current => IsLight ? ElementTheme.Light : ElementTheme.Dark;

        public static void Apply(FrameworkElement? root)
        {
            if (root is null) return;
            root.RequestedTheme = Current;
        }

        public static T Get<T>(string key)
        {
            var name = IsLight ? "Light" : "Dark";
            if (Application.Current.Resources.ThemeDictionaries[name] is ResourceDictionary dict
                && dict.ContainsKey(key) && dict[key] is T typed)
                return typed;
            return (T)Application.Current.Resources[key];
        }

        public static Brush Brush(string key) => Get<Brush>(key);
    }
}
