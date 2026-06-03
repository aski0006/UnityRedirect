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
                    _instance = Resources.Load<RouteCore>("RouteCore");
                    if (_instance != null)
                    {
                        _instance.RebuildGlobalInterceptors();
                    }
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
                _instance = Resources.Load<RouteCore>("RouteCore");
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

            for (var i = 0; i < globalInterceptorConfigs.Count; i++)
            {
                var config = globalInterceptorConfigs[i];
                if (config == null || !config.Enabled) continue;

                var interceptor = config.CreateInstance();
                if (interceptor != null)
                {
                    _globalInterceptors.Add(interceptor);
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
    }

    /// <summary>
    /// 拦截器序列化配置。
    /// </summary>
    [System.Serializable]
    public sealed class InterceptorConfig
    {
        [SerializeField] private string typeName;
        [SerializeField] private bool enabled = true;

        /// <summary>拦截器完整类型名</summary>
        public string TypeName => typeName;

        /// <summary>是否启用</summary>
        public bool Enabled => enabled;

        /// <summary>创建拦截器实例</summary>
        public INavigationInterceptorBase CreateInstance()
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = System.Type.GetType(typeName);
            if (type == null) return null;

            return System.Activator.CreateInstance(type) as INavigationInterceptorBase;
        }
    }
}
