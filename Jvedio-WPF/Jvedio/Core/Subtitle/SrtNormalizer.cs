using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jvedio.Core.Subtitle
{
    /// <summary>
    /// SRT 字幕规范化（移植自 JavBoss subtitle/srt.go，MIT 语义）：
    /// 丢弃畸形 cue、剥离 HTML/ASS 标记、重编号、统一时间轴格式。
    /// 输出 UTF-8 SRT；无有效 cue 返回 false。下载的字幕不可信，先过这里再落盘
    /// </summary>
    public static class SrtNormalizer
    {
        public const int MAX_BYTES = 2 << 20;

        private static readonly Regex Timing = new Regex(
            @"^(\d{2,3}):([0-5]\d):([0-5]\d)[,.](\d{3})\s*-->\s*(\d{2,3}):([0-5]\d):([0-5]\d)[,.](\d{3})$", RegexOptions.Compiled);
        private static readonly Regex Markup = new Regex(@"<[^>]*>|\{[^}]*\}", RegexOptions.Compiled);
        private static readonly Regex BlockSplit = new Regex(@"\n[\t ]*\n+", RegexOptions.Compiled);

        private static long TimestampMS(string h, string m, string s, string ms)
        {
            return ((long.Parse(h) * 60 + long.Parse(m)) * 60 + long.Parse(s)) * 1000 + long.Parse(ms);
        }

        public static bool TryNormalize(byte[] data, out byte[] normalized)
        {
            normalized = null;
            if (data == null || data.Length == 0 || data.Length > MAX_BYTES)
                return false;
            // 只收 UTF-8（BOM 剥掉）；全零字节或非法 UTF-8 视为损坏
            string raw;
            try {
                if (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                    raw = new UTF8Encoding(false).GetString(data, 3, data.Length - 3);
                else
                    raw = new UTF8Encoding(false, throwOnInvalidBytes: false).GetString(data);
                if (raw.Contains('\0'))
                    return false;
            } catch { return false; }

            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n").TrimStart('\ufeff').Trim();
            StringBuilder builder = new StringBuilder(raw.Length);
            int count = 0;
            foreach (string block in BlockSplit.Split(raw)) {
                string trimmed = block.Trim();
                if (trimmed.Length == 0)
                    continue;
                string[] lines = trimmed.Split('\n');
                if (lines.Length < 2 || !int.TryParse(lines[0].Trim(), out _))
                    continue;
                Match m = Timing.Match(lines[1].Trim());
                if (!m.Success)
                    continue;
                long start = TimestampMS(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                long end = TimestampMS(m.Groups[5].Value, m.Groups[6].Value, m.Groups[7].Value, m.Groups[8].Value);
                // 时间倒挂/零时长/空文本的 cue 不能渲染，丢弃
                if (start >= end || lines.Length == 2)
                    continue;
                string text = Markup.Replace(System.Net.WebUtility.HtmlDecode(string.Join("\n", lines.Skip(2))), "");
                text = text.Replace("\\", "");
                string[] kept = text.Split('\n').Where(l => l.Trim().Length > 0).ToArray();
                if (kept.Length == 0)
                    continue;
                count++;
                builder.Append(count).Append('\n')
                    .Append(m.Groups[1].Value).Append(':').Append(m.Groups[2].Value).Append(':').Append(m.Groups[3].Value).Append(',').Append(m.Groups[4].Value)
                    .Append(" --> ")
                    .Append(m.Groups[5].Value).Append(':').Append(m.Groups[6].Value).Append(':').Append(m.Groups[7].Value).Append(',').Append(m.Groups[8].Value)
                    .Append('\n').Append(string.Join("\n", kept)).Append("\n\n");
            }
            if (count == 0)
                return false;
            normalized = new UTF8Encoding(false).GetBytes(builder.ToString());
            return true;
        }
    }
}
