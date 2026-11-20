using System;

namespace RelaxValley.Framework.UI;

/// <summary>需要由宿主菜单注入“关闭自己”动作的视图模型。</summary>
public interface ICloseAware
{
    void AttachClose(Action closeSelf);
}
