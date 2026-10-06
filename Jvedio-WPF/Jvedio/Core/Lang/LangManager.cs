using System;
using System.Windows;
using System.Linq;
using System.Text.RegularExpressions;
using static Jvedio.App;

namespace Jvedio.Core.Lang
{
    public static class LangManager
    {
        public static void NormalizeShortcutBrackets()
        {
            // SuperControls 内置语言无法直接修改；字典内转换使显示和按文案查找共用同一值。
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries) {
                if (dictionary.Source == null ||
                    dictionary.Source.OriginalString.IndexOf("/Lang/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                foreach (object key in dictionary.Keys.Cast<object>().ToList()) {
                    if (!(dictionary[key] is string value))
                        continue;
                    string normalized = Regex.Replace(value, @"[（(]([A-Za-z])[）)]", "（$1）");
                    if (normalized != value)
                        dictionary[key] = normalized;
                }
            }
        }

        public static bool SetLang(string lang)
        {
            if (!SuperControls.Style.LangManager.SupportLanguages.Contains(lang)) {
                Logger.Warn($"lang not support: {lang}");
                return false;
            }

            string format = "pack://application:,,,/Jvedio;Component/Core/Lang/{0}.xaml";
            format = string.Format(format, lang);
            foreach (ResourceDictionary mergedDictionary in Application.Current.Resources.MergedDictionaries) {
                if (mergedDictionary.Source != null && mergedDictionary.Source.OriginalString.IndexOf("Jvedio;Component/Core/Lang", StringComparison.OrdinalIgnoreCase) >= 0) {
                    try {
                        bool flag = Application.Current.Resources.MergedDictionaries.Remove(mergedDictionary);
                        mergedDictionary.Source = new Uri(format, UriKind.RelativeOrAbsolute);
                        Application.Current.Resources.MergedDictionaries.Add(mergedDictionary);
                        NormalizeShortcutBrackets();
                        return true;
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        return false;
                    }
                }
            }

            NormalizeShortcutBrackets();
            return true;
        }
    }
}
