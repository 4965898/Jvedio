using Microsoft.WindowsAPICodePack.Dialogs;
using System;

namespace Jvedio.Core.Utils
{
    /// <summary>
    /// 现代文件夹选择助手：WindowsAPICodePack 的 CommonOpenFileDialog（Vista+ IFileDialog），
    /// 与资源管理器同款——有地址栏（可粘贴路径）、收藏夹/快速访问、可新建文件夹、可调大小。
    /// 取代 WinForms FolderBrowserDialog（小窗口树形、不能粘贴路径，用户反馈不人性化）。
    /// COM 异常时回退 WinForms 老对话框，保证任何环境都能选
    /// </summary>
    public static class FolderPickerHelper
    {
        /// <summary>选择文件夹。取消返回 null；title 用现有语言键；initialPath 为当前值（存在则定位过去）；owner 为宿主窗口句柄（模态归属）</summary>
        public static string PickFolder(string initialPath, string title, IntPtr owner = default)
        {
            // 现代对话框优先
            try {
                using (CommonOpenFileDialog dialog = new CommonOpenFileDialog()) {
                    dialog.IsFolderPicker = true;
                    if (!string.IsNullOrEmpty(title))
                        dialog.Title = title;
                    if (!string.IsNullOrWhiteSpace(initialPath) && System.IO.Directory.Exists(initialPath))
                        dialog.InitialDirectory = initialPath;
                    dialog.EnsurePathExists = true;
                    dialog.AllowNonFileSystemItems = false;
                    CommonFileDialogResult result =
                        owner != IntPtr.Zero ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                    if (result == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                        return dialog.FileName;
                    return null; // 用户取消
                }
            } catch (Exception ex) {
                Jvedio.App.Logger?.Error($"现代文件夹选择器异常，回退旧对话框：{ex.Message}");
            }

            // 回退：WinForms 老对话框（老系统/COM 失败时仍可用）
            try {
                using (System.Windows.Forms.FolderBrowserDialog fallback = new System.Windows.Forms.FolderBrowserDialog()) {
                    if (!string.IsNullOrEmpty(title))
                        fallback.Description = title;
                    if (!string.IsNullOrWhiteSpace(initialPath) && System.IO.Directory.Exists(initialPath))
                        fallback.SelectedPath = initialPath;
                    if (fallback.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        return fallback.SelectedPath;
                }
            } catch (Exception ex) {
                Jvedio.App.Logger?.Error($"文件夹选择器失败：{ex.Message}");
            }
            return null;
        }
    }
}
