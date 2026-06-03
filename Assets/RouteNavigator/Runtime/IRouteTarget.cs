using System.Collections;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 非泛型标记接口。用于判断目标是否支持路由回调。
    /// </summary>
    public interface IRouteTarget
    {
    }

    /// <summary>
    /// 泛型路由目标接口。
    /// 挂载在目标 GameObject 上，路由到达后自动调用回调。
    /// </summary>
    public interface IRouteTarget<TData> : IRouteTarget
        where TData : struct
    {
        /// <summary>
        /// [同步] 路由到达目标后立即调用。
        /// 应轻量快速，不阻塞管道（如 SetActive、显示加载状态）。
        /// </summary>
        void OnNavigateTo(TData data);

        /// <summary>
        /// [协程] 启动异步后处理。
        /// 目标自身作为 MonoBehaviour 启动协程管理生命周期，
        /// 当目标 Destory 时协程自动停止。
        /// 用于网络请求、IO 加载、延时动画等耗时操作。
        /// </summary>
        void StartPostProcess(TData data);
    }
}
