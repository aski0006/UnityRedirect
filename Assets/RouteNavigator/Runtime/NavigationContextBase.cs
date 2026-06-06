using UnityEngine;

namespace Kogane.RouteNavigator {
    /// <summary>
    /// 非泛型导航上下文基类。
    /// 通用拦截器（INavigationInterceptorBase）通过此基类只读访问导航信息。
    /// </summary>
    public class NavigationContextBase {
        /// <summary>路由标识</summary>
        public string RouteId { get; protected set; }

        /// <summary>
        /// 取消标志。
        /// 设为 true 后管道将中断，后续拦截器不再执行。
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>导航结果。拦截器可累积写入。</summary>
        public NavigationResult Result { get; set; }

        /// <summary>路由参数的 JSON 只读视图（通用拦截器使用）</summary>
        public string RawData { get; protected set; }

        /// <summary>发起导航的源物体</summary>
        public GameObject Source { get; protected set; }
        /// <summary>当前导航版本号</summary>
        public int Version { get; internal set; }
    }
}
