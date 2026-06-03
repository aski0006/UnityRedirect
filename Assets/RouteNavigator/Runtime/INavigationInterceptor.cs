using System.Collections;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 拦截器排序常量。Order 从小到大执行。
    /// </summary>
    public static class InterceptorOrders
    {
        /// <summary>最先执行（日志、埋点等基础设施）</summary>
        public const int First = -1000;

        /// <summary>默认顺序</summary>
        public const int Default = 0;

        /// <summary>通用检查（权限、条件等）</summary>
        public const int Early = -100;

        /// <summary>目标解析</summary>
        public const int ResolveTarget = 500;

        /// <summary>最后执行</summary>
        public const int Last = 1000;
    }

    /// <summary>
    /// 非泛型拦截器标记接口。
    /// 所有拦截器须实现此接口或其泛型变体。
    /// </summary>
    public interface INavigationInterceptor
    {
        /// <summary>
        /// 执行顺序。Order 从小到大依次执行。
        /// </summary>
        int Order { get; }
    }

    /// <summary>
    /// 非泛型拦截器接口。
    /// 实现此接口的拦截器可作用于所有 TData 类型的导航。
    /// 只能通过 NavigationContextBase 访问 JSON 只读视图，不可修改 Data。
    /// </summary>
    public interface INavigationInterceptorBase : INavigationInterceptor
    {
        /// <summary>
        /// 执行导航拦截逻辑。
        /// 通过 yield break 同步返回，或 yield return 异步等待。
        /// </summary>
        IEnumerator OnNavigate(NavigationContextBase context);
    }

    /// <summary>
    /// 泛型拦截器接口。
    /// 实现此接口的拦截器仅作用于指定 TData 类型的导航。
    /// 通过 NavigationContext{TData}.Data 强类型访问参数，零装箱。
    /// </summary>
    public interface INavigationInterceptor<TData> : INavigationInterceptor
        where TData : struct
    {
        /// <summary>
        /// 执行导航拦截逻辑。
        /// 通过 yield break 同步返回，或 yield return 异步等待。
        /// </summary>
        IEnumerator OnNavigate(NavigationContext<TData> context);
    }
}
