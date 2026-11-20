using Microsoft.Xna.Framework;

namespace RelaxValley.Framework;

/// <summary>
/// Iconic Framework（furyx639.ToolbarIcons）的工具图标注册 API。
/// 签名取自 PerfectionHandbook 官方源码的集成接口，只声明用到的成员。
/// </summary>
public interface IIconicFrameworkApi
{
    /// <summary>注册一个工具图标。</summary>
    /// <param name="id">图标的唯一标识（用模组 UniqueID）。</param>
    /// <param name="texturePath">图标贴图路径。</param>
    /// <param name="sourceRect">贴图里的图标区域。</param>
    /// <param name="getTitle">悬浮 / 环形菜单里显示的标题。</param>
    /// <param name="getDescription">悬浮说明文字。</param>
    /// <param name="onClick">左键点击动作。</param>
    /// <param name="onRightClick">右键点击动作（可选）。</param>
    void AddToolbarIcon(
        string id,
        string texturePath,
        Rectangle? sourceRect,
        Func<string>? getTitle,
        Func<string>? getDescription,
        Action onClick,
        Action? onRightClick = null);
}
