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
        private static bool _persistenceLoaded;
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
        /// 宿主应是一个常驻对象（如游戏管理器），导航协程将附着于此。
        /// 宿主会被设为 DontDestroyOnLoad，跨场景不丢失。
        /// </summary>
        public static void Initialize(MonoBehaviour coroutineHost)
        {
            if (coroutineHost == null)
            {
                Debug.LogError("[RouteNavigator] 协程宿主不能为 null");
                return;
            }

            _coroutineHost = coroutineHost;

            // 设为常驻对象，跨场景不丢失
            var hostGameObject = coroutineHost.gameObject;
            if (hostGameObject.scene.buildIndex != -1) // 非 DontDestroyOnLoad 场景
            {
                UnityEngine.Object.DontDestroyOnLoad(hostGameObject);
            }

            // 加载持久化配置
            LoadPersistence();
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
            Navigate(routeId, data, null, onComplete);
        }

        /// <summary>
        /// 发起导航（带源物体）。
        /// </summary>
        /// <param name="routeId">路由标识</param>
        /// <param name="data">路由参数（值类型 struct）</param>
        /// <param name="source">发起导航的源物体</param>
        /// <param name="onComplete">导航完成回调（成功/失败均触发）</param>
        public static void Navigate(
            string routeId,
            TData data,
            GameObject source,
            Action<NavigationResult> onComplete = null)
        {
            // 确保持久化已加载
            if (!_persistenceLoaded)
            {
                LoadPersistence();
            }

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

            var context = new NavigationContext<TData>(routeId, data, source, version);
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
                _persistenceLoaded = false;
            }
        }

        /// <summary>
        /// 从 RouteRegistry ScriptableObject 加载持久化的拦截器配置。
        /// </summary>
        private static void LoadPersistence()
        {
            if (_persistenceLoaded) return;

            lock (_lock)
            {
                if (_persistenceLoaded) return;

                var registry = Resources.Load<RouteRegistry>("RouteRegistry");
                if (registry != null)
                {
                    var persistedInterceptors = registry.LoadTypedInterceptors<TData>();
                    for (var i = 0; i < persistedInterceptors.Count; i++)
                    {
                        _typedInterceptors.Add(persistedInterceptors[i]);
                    }
                    _typedInterceptors.Sort((a, b) => a.Order.CompareTo(b.Order));
#if UNITY_EDITOR
                    Debug.Log(
                        $"[RouteNavigator] 从 RouteRegistry 恢复 {persistedInterceptors.Count} 个 " +
                        $"类型专属拦截器 (TData={typeof(TData).Name})");
#endif
                }

                _persistenceLoaded = true;
            }
        }

        /// <summary>
        /// 刷新持久化配置（编辑器保存配置后调用）。
        /// </summary>
        /// <summary>
    /// 刷新持久化配置（编辑器保存配置后调用）。
    /// 注意：此操作会丢弃当前持有的所有类型专属拦截器（包括运行时通过 RegisterInterceptor
    /// 动态注册的），然后从 RouteRegistry 资产重新加载。如果需要在编辑器中保留运行时注册的
    /// 拦截器，请先将它们写入 RouteRegistry 资产。
    /// </summary>
    internal static void ReloadPersistence()
    {
        lock (_lock)
        {
            _typedInterceptors.RemoveAll(i => !IsRuntimeRegistered(i));
            _persistenceLoaded = false;
        }
        LoadPersistence();

        lock (_lock)
        {
            _typedInterceptors.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
            LoadPersistence();

            lock (_lock)
            {
                _typedInterceptors.Sort((a, b) => a.Order.CompareTo(b.Order));
            }
        }

        /// <summary>判断拦截器是否在运行时注册的（而非持久化加载的），暂无法精确区分，保留所有</summary>
        private static bool IsRuntimeRegistered(INavigationInterceptor<TData> interceptor)
        {
            return false; // 简化：ReloadPersistence 时全部从持久化重新加载
        }

        private static IEnumerator ExecutePipeline(
            NavigationContext<TData> ctx,
            int version,
            Action<NavigationResult> onComplete)
        {
            try
            {
                // ── 执行通用拦截器 ──
                var core = RouteCore.Instance;
                if (core != null)
                {
                    var globalInterceptors = core.GlobalInterceptors;
                    for (var i = 0; i < globalInterceptors.Count; i++)
                    {
                        var interceptor = globalInterceptors[i];

                        if (!IsCurrentVersion(version, ctx)) yield break;

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

                        if (ctx.Cancel) yield break;
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

                    if (!IsCurrentVersion(version, ctx)) yield break;

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

                    if (ctx.Cancel) yield break;
                }

                // ── 管道完成 ──
                // 结果完全由拦截器链负责设定
                // 若无拦截器设置结果，Success 默认为 false
#if UNITY_EDITOR
                if (ctx.Result.Success)
                {
                    Debug.Log(
                        $"[Route] 导航完成: {ctx.RouteId}, " +
                        $"目标: {ctx.Result.TargetObject?.name}");
                }
                else if (!ctx.Cancel)
                {
                    Debug.LogWarning(
                        $"[Route] 导航未设置成功状态: {ctx.RouteId}");
                }
#endif
            }
            finally
            {
                // 统一触发回调并清理
                onComplete?.Invoke(ctx.Result);
                DispatchResult(ctx.RouteId, ctx.Data, ctx.Result);
                _currentCoroutine = null;
            }
        }

        /// <summary>
        /// 检查当前版本号是否仍是最新。
        /// </summary>
        private static bool IsCurrentVersion(int version, NavigationContext<TData> ctx)
        {
            if (_currentVersion != version)
            {
                ctx.Result = NavigationResult.Cancelled();
                return false;
            }
            return true;
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
