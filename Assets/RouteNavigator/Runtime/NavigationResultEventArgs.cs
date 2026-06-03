using System;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 导航结果事件参数。
    /// Data 属性允许一次装箱，仅用于埋点/调试等非常规路径。
    /// </summary>
    public sealed class NavigationResultEventArgs : EventArgs
    {
        /// <summary>路由标识</summary>
        public string RouteId { get; init; }

        /// <summary>
        /// 路由参数（值类型会装箱，仅用于埋点/调试）。
        /// </summary>
        public object Data { get; init; }

        /// <summary>导航结果</summary>
        public NavigationResult Result { get; init; }

        /// <summary>导航完成时的时间戳（毫秒）</summary>
        public long TimestampMs { get; init; }
    }
}
