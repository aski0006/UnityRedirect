using System.Collections;
using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 通用日志拦截器。
    /// 记录所有导航的开始信息，不依赖具体 TData 类型。
    /// </summary>
    public sealed class LoggingInterceptor : INavigationInterceptorBase
    {
        /// <summary>最先执行</summary>
        public int Order => InterceptorOrders.First;

        public IEnumerator OnNavigate(NavigationContextBase context)
        {
#if UNITY_EDITOR
            Debug.Log(
                $"[Route] 导航开始: {context.RouteId}, " +
                $"Version={context.Version}, Data={context.RawData}");
#endif
            yield break;
        }
    }
}
