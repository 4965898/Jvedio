using System.Collections.Generic;
using System.Text;

namespace Jvedio.Core.Utils
{
    /// <summary>
    /// 演员英文名（罗马字）转换工具：
    /// 1. SlugToDisplay：JavDB 演员页 URL slug（如 mikami-yua）→ 显示名（Mikami Yua）
    /// 2. Convert：假名（ひらがな/カタカナ）→ 罗马字（Hepburn），汉字原样保留（兜底用）
    /// </summary>
    public static class RomajiConverter
    {
        // 基本清音 + 浊音/半浊音
        private static readonly Dictionary<char, string> Kana = new Dictionary<char, string>() {
            { 'あ', "a" }, { 'い', "i" }, { 'う', "u" }, { 'え', "e" }, { 'お', "o" },
            { 'か', "ka" }, { 'き', "ki" }, { 'く', "ku" }, { 'け', "ke" }, { 'こ', "ko" },
            { 'さ', "sa" }, { 'し', "shi" }, { 'す', "su" }, { 'せ', "se" }, { 'そ', "so" },
            { 'た', "ta" }, { 'ち', "chi" }, { 'つ', "tsu" }, { 'て', "te" }, { 'と', "to" },
            { 'な', "na" }, { 'に', "ni" }, { 'ぬ', "nu" }, { 'ね', "ne" }, { 'の', "no" },
            { 'は', "ha" }, { 'ひ', "hi" }, { 'ふ', "fu" }, { 'へ', "he" }, { 'ほ', "ho" },
            { 'ま', "ma" }, { 'み', "mi" }, { 'む', "mu" }, { 'め', "me" }, { 'も', "mo" },
            { 'や', "ya" }, { 'ゆ', "yu" }, { 'よ', "yo" },
            { 'ら', "ra" }, { 'り', "ri" }, { 'る', "ru" }, { 'れ', "re" }, { 'ろ', "ro" },
            { 'わ', "wa" }, { 'を', "wo" }, { 'ん', "n" },
            { 'が', "ga" }, { 'ぎ', "gi" }, { 'ぐ', "gu" }, { 'げ', "ge" }, { 'ご', "go" },
            { 'ざ', "za" }, { 'じ', "ji" }, { 'ず', "zu" }, { 'ぜ', "ze" }, { 'ぞ', "zo" },
            { 'だ', "da" }, { 'ぢ', "ji" }, { 'づ', "zu" }, { 'で', "de" }, { 'ど', "do" },
            { 'ば', "ba" }, { 'び', "bi" }, { 'ぶ', "bu" }, { 'べ', "be" }, { 'ぼ', "bo" },
            { 'ぱ', "pa" }, { 'ぴ', "pi" }, { 'ぷ', "pu" }, { 'ぺ', "pe" }, { 'ぽ', "po" },
            // 小写拗音标记
            { 'ゃ', "ya" }, { 'ゅ', "yu" }, { 'ょ', "yo" },
        };

        // 拗音组合：前一个假名 + 小写ゃゅょ
        private static readonly Dictionary<string, string> Yōon = new Dictionary<string, string>() {
            { "きゃ", "kya" }, { "きゅ", "kyu" }, { "きょ", "kyo" },
            { "しゃ", "sha" }, { "しゅ", "shu" }, { "しょ", "sho" },
            { "ちゃ", "cha" }, { "ちゅ", "chu" }, { "ちょ", "cho" },
            { "にゃ", "nya" }, { "にゅ", "nyu" }, { "にょ", "nyo" },
            { "ひゃ", "hya" }, { "ひゅ", "hyu" }, { "ひょ", "hyo" },
            { "みゃ", "mya" }, { "みゅ", "myu" }, { "みょ", "myo" },
            { "りゃ", "rya" }, { "りゅ", "ryu" }, { "りょ", "ryo" },
            { "ぎゃ", "gya" }, { "ぎゅ", "gyu" }, { "ぎょ", "gyo" },
            { "じゃ", "ja" }, { "じゅ", "ju" }, { "じょ", "jo" },
            { "びゃ", "bya" }, { "びゅ", "byu" }, { "びょ", "byo" },
            { "ぴゃ", "pya" }, { "ぴゅ", "pyu" }, { "ぴょ", "pyo" },
        };

        /// <summary>
        /// JavDB 演员 slug → 显示名：mikami-yua → Mikami Yua
        /// </summary>
        public static string SlugToDisplay(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return slug;
            string[] parts = slug.Split('-');
            for (int i = 0; i < parts.Length; i++) {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// 假名 → 罗马字（Hepburn）：えいみ → eimi、キララ → kirara。
        /// 汉字/拉丁字符原样保留（汉字读音需词典，本方法只做假名兜底）。
        /// </summary>
        public static string Convert(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            StringBuilder sb = new StringBuilder();
            string lastRomaji = ""; // 上一个假名对应的罗马字（拗音替换用）
            for (int i = 0; i < name.Length; i++) {
                char c = name[i];
                // 拗音：前一个假名 + 小写ゃゅょ
                if ((c == 'ゃ' || c == 'ゅ' || c == 'ょ') && i > 0) {
                    string prev = name[i - 1].ToString() + c;
                    if (Yōon.TryGetValue(prev, out string yo)) {
                        if (sb.Length >= lastRomaji.Length)
                            sb.Length -= lastRomaji.Length;
                        sb.Append(yo);
                        lastRomaji = yo;
                        continue;
                    }
                }
                // 促音 っ：双写下一个辅音
                if (c == 'っ') {
                    if (i + 1 < name.Length && Kana.TryGetValue(name[i + 1], out string next))
                        sb.Append(next[0]);
                    lastRomaji = "";
                    continue;
                }
                // 长音 ー：重复前一个元音
                if (c == 'ー') {
                    if (sb.Length > 0) {
                        char last = sb[sb.Length - 1];
                        if ("aiueo".IndexOf(last) >= 0)
                            sb.Append(last);
                    }
                    lastRomaji = "";
                    continue;
                }
                if (Kana.TryGetValue(c, out string r)) {
                    sb.Append(r);
                    lastRomaji = r;
                } else {
                    sb.Append(c);
                    lastRomaji = "";
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 转换结果是否为纯 ASCII（可视为英文名）；含汉字/假名残留则不是完整罗马字
        /// </summary>
        public static bool IsPureAscii(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (char c in s) {
                if (c > 127)
                    return false;
            }
            return true;
        }
    }
}