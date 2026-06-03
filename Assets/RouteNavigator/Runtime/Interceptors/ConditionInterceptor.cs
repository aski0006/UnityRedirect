using System;
using System.Collections;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 条件拦截器。
    /// 根据路由参数 TData 检查导航条件是否满足。
    /// </summary>
    public sealed class ConditionInterceptor<TData> : INavigationInterceptor<TData>
        where TData : struct
    {
        private readonly Func<TData, bool> _condition;
        private readonly string _failMessage;

        /// <summary>
        /// 构造一个条件拦截器。
        /// </summary>
        /// <param name="condition">条件函数，返回 true 允许导航继续</param>
        /// <param name="failMessage">条件不满足时的失败消息</param>
        public ConditionInterceptor(
            Func<TData, bool> condition,
            string failMessage = "条件不满足，导航已取消")
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _failMessage = failMessage ?? "条件不满足，导航已取消";
        }

        /// <summary>在通用日志之后、目标解析之前执行</summary>
        public int Order => InterceptorOrders.Early;

        public IEnumerator OnNavigate(NavigationContext<TData> context)
        {
            try
            {
                if (!_condition(context.Data))
                {
                    context.Cancel = true;
                    context.Result = NavigationResult.Failed(_failMessage);
                }
            }
            catch (Exception e)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed(
                    $"条件拦截器异常: {e.Message}");
            }

            yield break;
        }
    }
}
