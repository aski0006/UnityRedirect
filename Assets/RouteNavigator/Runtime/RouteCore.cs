using System.Collections.Generic;
using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 非泛型路由核心。
    /// 管理路由定义表和通用拦截器，供 RouteNavigator{TData} 调用。
    /// 拦截器配置通过 ScriptableObject 持久化，域重载后自动恢复。
    /// </summary>
    public sealed class RouteCore : ScriptableObject
    {
        private static RouteCore _instance;

        [Header("路由定义表")]
        [SerializeField] private List<RouteDefinition> routes = new();

        [Header("通用拦截器配置")]
        [SerializeField] private List<InterceptorConfig> globalInterceptorConfigs = new();

        private readonly List<INavigationInterceptorBase> _globalInterceptors = new();

        /// <summary>单例实例</summary>
        public static RouteCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<RouteCore>("RouteNavigatorDatabase/RouteCore");
                    if (_instance != null)
                    {
                        _instance.RebuildGlobalInterceptors();
                    }
#if UNITY_EDITOR
                    else
                    {
                        Debug.LogWarning(
                            "[RouteCore] 未找到 RouteCore 资产。请创建 RouteCore 并放置于 Resources 文件夹。");
                    }
#endif
                }
                return _instance;
            }
        }

        /// <summary>所有路由定义</summary>
        public IReadOnlyList<RouteDefinition> Routes => routes;

        /// <summary>通用拦截器列表</summary>
        public IReadOnlyList<INavigationInterceptorBase> GlobalInterceptors
            => _globalInterceptors;

        /// <summary>根据 routeId 查找路由定义</summary>
        public RouteDefinition GetRoute(string routeId)
        {
            if (string.IsNullOrEmpty(routeId)) return null;

            for (var i = 0; i < routes.Count; i++)
            {
                if (routes[i] != null && routes[i].RouteId == routeId)
                {
                    return routes[i];
                }
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 触发静态构造器和单例加载
            if (_instance == null)
            {
                _instance = Resources.Load<RouteCore>("RouteNavigatorDatabase/RouteCore");
                if (_instance != null)
                {
                    _instance.RebuildGlobalInterceptors();
                }
            }
        }

        private void OnEnable()
        {
            if (_instance == null)
            {
                _instance = this;
                RebuildGlobalInterceptors();
            }
        }

        private void RebuildGlobalInterceptors()
        {
            _globalInterceptors.Clear();

            if (globalInterceptorConfigs == null) return;

            for (var i = 0; i < globalInterceptorConfigs.Count; i++)
            {
                var config = globalInterceptorConfigs[i];
                if (config == null || !config.Enabled) continue;

                var interceptor = config.CreateBaseInstance();
                if (interceptor != null)
                {
                    // 应用 orderOverride
                    if (config.HasOrderOverride)
                    {
                        _globalInterceptors.Add(
                            new OrderedInterceptorWrapper(interceptor, config.OrderOverrideValue));
                    }
                    else
                    {
                        _globalInterceptors.Add(interceptor);
                    }
                }
            }

            // 按 Order 排序
            _globalInterceptors.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        /// <summary>设置路由表（编辑器用）</summary>
        internal void SetRoutes(List<RouteDefinition> routeList)
        {
            routes = routeList;
        }

        /// <summary>设置通用拦截器配置（编辑器用）</summary>
        internal void SetGlobalInterceptorConfigs(List<InterceptorConfig> configs)
        {
            globalInterceptorConfigs = configs;
            RebuildGlobalInterceptors();
        }

        /// <summary>
        /// 以 OrderOverride 值包装拦截器，覆盖其原始 Order。
        /// </summary>
        private sealed class OrderedInterceptorWrapper : INavigationInterceptorBase
        {
            private readonly INavigationInterceptorBase _inner;
            public int Order { get; }

            public OrderedInterceptorWrapper(INavigationInterceptorBase inner, int order)
            {
                _inner = inner;
                Order = order;
            }

            public System.Collections.IEnumerator OnNavigate(NavigationContextBase context)
            {
                return _inner.OnNavigate(context);
            }
        }
    }

    /// <summary>
    /// 拦截器序列化配置。
    /// </summary>
    [System.Serializable]
    public sealed class InterceptorConfig
    {
        [SerializeField] private string typeName;
        [SerializeField] private bool enabled = true;
        [SerializeField] private int orderOverride;

        /// <summary>拦截器完整类型名</summary>
        public string TypeName => typeName;

        /// <summary>是否启用</summary>
        public bool Enabled => enabled;

        /// <summary>是否有 Order 覆盖值</summary>
        public bool HasOrderOverride => orderOverride != 0;

        /// <summary>Order 覆盖值（0 表示使用代码中的 Order）</summary>
        public int OrderOverrideValue => orderOverride;

        /// <summary>
        /// 创建通用拦截器实例（实现 INavigationInterceptorBase 的类型）。
        /// </summary>
        public INavigationInterceptorBase CreateBaseInstance()
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = System.Type.GetType(typeName);
            if (type == null) return null;

            return System.Activator.CreateInstance(type) as INavigationInterceptorBase;
        }

        /// <summary>
        /// 创建类型专属拦截器实例（实现 INavigationInterceptor{TData} 的类型）。
        /// </summary>
        public INavigationInterceptor<TData> CreateTypedInstance<TData>()
            where TData : struct
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = System.Type.GetType(typeName);
            if (type == null) return null;

            return System.Activator.CreateInstance(type) as INavigationInterceptor<TData>;
        }
    }
}

