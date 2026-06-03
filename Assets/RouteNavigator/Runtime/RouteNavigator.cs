using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 泛型导航入口。
    /// 作为 RouteCore 的薄包装层，管理 TData 专属拦截器。
    /// </summary>
    public static class RouteNavigator<TData>
        where TData : struct
    {
        private static int _currentVersion;
        private static Coroutine _currentCoroutine;
        private static MonoBehaviour _coroutineHost;
        private static readonly List<INavigationInterceptor<TData>> _typedInterceptors = new();

        private static readonly object _lock = new();

        /// <summary>
        /// 当前导航版本号。每次 Navigate 调用递增。
        /// 目标后处理通过对比此值判断是否"过时"。
        /// </summary>
        public static int CurrentVersion => _currentVersion;

        /// <summary>
        /// 全局导航结果事件（埋点/调试用）。
        /// </summary>
        public static event Action<NavigationResultEventArgs> OnNavigationResult;

        /// <summary>
        /// 初始化导航器，设置协程宿主。
        /// 应在游戏启动时调用一次（如游戏管理器 Awake）。
        /// </summary>
        public static void Initialize(MonoBehaviour coroutineHost)
        {
            if (coroutineHost == null)
            {
                Debug.LogError("[RouteNavigator] 协程宿主不能为 null");
                return;
            }
            _coroutineHost = coroutineHost;
        }

        /// <summary>
        /// 发起导航。
        /// </summary>
        /// <param name="routeId">路由标识</param>
        /// <param name="data">路由参数（值类型 struct）</param>
        /// <param name="onComplete">导航完成回调（成功/失败均触发）</param>
        public static void Navigate(
            string routeId,
            TData data,
            Action<NavigationResult> onComplete = null)
        {
            if (_coroutineHost == null)
            {
                Debug.LogError(
                    "[RouteNavigator] 未初始化。请先调用 Initialize(MonoBehaviour)。");
                var errorResult = NavigationResult.Failed("RouteNavigator 未初始化");
                onComplete?.Invoke(errorResult);
                DispatchResult(routeId, data, errorResult);
                return;
            }

            if (string.IsNullOrEmpty(routeId))
            {
                var errorResult = NavigationResult.Failed("routeId 不能为空");
                onComplete?.Invoke(errorResult);
                DispatchResult(routeId, data, errorResult);
                return;
            }

            int version;
            lock (_lock)
            {
                _currentVersion++;
                version = _currentVersion;
            }

            // 停止上一次导航
            if (_currentCoroutine != null)
            {
                _coroutineHost.StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }

            var context = new NavigationContext<TData>(routeId, data, null, version);
            _currentCoroutine = _coroutineHost.StartCoroutine(
                ExecutePipeline(context, version, onComplete));
        }

        /// <summary>
        /// 注册一个类型专属拦截器。
        /// </summary>
        public static void RegisterInterceptor(INavigationInterceptor<TData> interceptor)
        {
            if (interceptor == null) return;

            lock (_lock)
            {
                _typedInterceptors.Add(interceptor);
                _typedInterceptors.Sort((a, b) => a.Order.CompareTo(b.Order));
            }
        }

        /// <summary>
        /// 移除指定类型的拦截器。
        /// </summary>
        public static void RemoveInterceptor<T>()
            where T : INavigationInterceptor<TData>
        {
            lock (_lock)
            {
                _typedInterceptors.RemoveAll(i => i is T);
            }
        }

        /// <summary>
        /// 重置导航器状态（测试用）。
        /// 清除所有注册的拦截器和版本号。
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _typedInterceptors.Clear();
                _currentVersion = 0;
                _currentCoroutine = null;
            }
        }

        private static IEnumerator ExecutePipeline(
            NavigationContext<TData> ctx,
            int version,
            Action<NavigationResult> onComplete)
        {
            // ── 执行通用拦截器 ──
            var core = RouteCore.Instance;
            if (core != null)
            {
                var globalInterceptors = core.GlobalInterceptors;
                for (var i = 0; i < globalInterceptors.Count; i++)
                {
                    var interceptor = globalInterceptors[i];

                    // 版本过期检查
                    if (_currentVersion != version)
                    {
                        ctx.Result = NavigationResult.Cancelled();
                        yield break;
                    }

                    try
                    {
                        yield return interceptor.OnNavigate(ctx);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(
                            $"[Route] 通用拦截器异常 [{interceptor.GetType().Name}]: {e.Message}");
                        ctx.Cancel = true;
                        ctx.Result = NavigationResult.Failed(
                            $"拦截器异常: {interceptor.GetType().Name}");
                        yield break;
                    }

                    if (ctx.Cancel)
                    {
                        yield break;
                    }
                }
            }

            // ── 执行类型专属拦截器 ──
            List<INavigationInterceptor<TData>> snapshot;
            lock (_lock)
            {
                snapshot = new List<INavigationInterceptor<TData>>(_typedInterceptors);
            }

            for (var i = 0; i < snapshot.Count; i++)
            {
                var interceptor = snapshot[i];

                // 版本过期检查
                if (_currentVersion != version)
                {
                    ctx.Result = NavigationResult.Cancelled();
                    yield break;
                }

                try
                {
                    yield return interceptor.OnNavigate(ctx);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[Route] 拦截器异常 [{interceptor.GetType().Name}]: {e.Message}");
                    ctx.Cancel = true;
                    ctx.Result = NavigationResult.Failed(
                        $"拦截器异常: {interceptor.GetType().Name}");
                    yield break;
                }

                if (ctx.Cancel)
                {
                    yield break;
                }
            }

            // ── 管道完成 ──
            if (!ctx.Result.Success && string.IsNullOrEmpty(ctx.Result.Message))
            {
                ctx.Result = NavigationResult.Succeeded(null);
            }

#if UNITY_EDITOR
            if (ctx.Result.Success)
            {
                Debug.Log(
                    $"[Route] 导航完成: {ctx.RouteId}, 目标: {ctx.Result.TargetObject?.name}");
            }
            else
            {
                Debug.LogWarning(
                    $"[Route] 导航失败: {ctx.RouteId}, 原因: {ctx.Result.Message}");
            }
#endif

            onComplete?.Invoke(ctx.Result);
            DispatchResult(ctx.RouteId, ctx.Data, ctx.Result);
            _currentCoroutine = null;
        }

        private static void DispatchResult(string routeId, TData data, NavigationResult result)
        {
            var args = new NavigationResultEventArgs
            {
                RouteId = routeId,
                Data = data,
                Result = result,
                TimestampMs = (long)(Time.realtimeSinceStartupAsDouble * 1000),
            };
            OnNavigationResult?.Invoke(args);
        }
    }
}
