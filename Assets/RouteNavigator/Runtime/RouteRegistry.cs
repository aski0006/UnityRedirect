using System.Collections.Generic;
using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 类型专属拦截器分组。
    /// 存储某个特定 TData 类型的拦截器持久化配置。
    /// </summary>
    [System.Serializable]
    public sealed class TypedInterceptorGroup
    {
        [SerializeField] private string dataTypeFullName;
        [SerializeField] private List<InterceptorConfig> interceptors = new();

        /// <summary>TData 类型的 FullName</summary>
        public string DataTypeFullName => dataTypeFullName;

        /// <summary>该类型下的拦截器配置列表</summary>
        public IReadOnlyList<InterceptorConfig> Interceptors => interceptors;

        public TypedInterceptorGroup(string typeFullName)
        {
            dataTypeFullName = typeFullName;
        }
    }

    /// <summary>
    /// 路由注册表资产。
    /// 存储所有 TData 类型的类型专属拦截器配置。
    /// 域重载后由 RouteNavigator{TData} 静态构造器自动加载恢复。
    ///
    /// 设计决策（与规范 v7 的差异说明）：
    /// 规范原设计为每个 TData 对应一个独立 RouteRegistry{TData} 泛型资产，
    /// 当前实现采用单一非泛型 RouteRegistry + TypedInterceptorGroup 分组。
    /// 理由：（1）减少资产数量，降低维护成本；
    /// （2）编辑器工具可在单个资产中管理所有类型的拦截器配置；
    /// （3）加载时通过 Type.FullName 匹配，与泛型方案效果等价。
    /// 路由定义表由 RouteCore 统一管理，此处不存储路由定义。
    /// </summary>
    [CreateAssetMenu(
        menuName = "Route Navigator/Registry",
        fileName = "RouteRegistry"
    )]
    public sealed class RouteRegistry : ScriptableObject
    {
        [SerializeField] private List<TypedInterceptorGroup> typedGroups = new();

        /// <summary>所有类型专属分组</summary>
        public IReadOnlyList<TypedInterceptorGroup> TypedGroups => typedGroups;

        /// <summary>
        /// 获取指定 TData 类型的拦截器组。
        /// </summary>
        public TypedInterceptorGroup GetGroup<TData>() where TData : struct
        {
            var typeName = typeof(TData).FullName;
            return GetGroup(typeName);
        }

        /// <summary>
        /// 获取指定类型名的拦截器组。
        /// </summary>
        public TypedInterceptorGroup GetGroup(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName)) return null;

            for (var i = 0; i < typedGroups.Count; i++)
            {
                if (typedGroups[i] != null &&
                    typedGroups[i].DataTypeFullName == typeFullName)
                {
                    return typedGroups[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 获取或创建指定 TData 类型的拦截器组。
        /// </summary>
        public TypedInterceptorGroup GetOrCreateGroup<TData>() where TData : struct
        {
            var group = GetGroup<TData>();
            if (group == null)
            {
                group = new TypedInterceptorGroup(typeof(TData).FullName);
                typedGroups.Add(group);
            }
            return group;
        }

        /// <summary>
        /// 从持久化配置加载指定 TData 类型的拦截器实例列表。
        /// </summary>
        internal List<INavigationInterceptor<TData>> LoadTypedInterceptors<TData>()
            where TData : struct
        {
            var result = new List<INavigationInterceptor<TData>>();
            var group = GetGroup<TData>();
            if (group == null) return result;

            var configs = group.Interceptors;
            for (var i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                if (config == null || !config.Enabled) continue;

                var interceptor = config.CreateTypedInstance<TData>();
                if (interceptor != null)
                {
                    if (config.HasOrderOverride)
                    {
                        result.Add(new TypedOrderedInterceptorWrapper<TData>(
                            interceptor, config.OrderOverrideValue));
                    }
                    else
                    {
                        result.Add(interceptor);
                    }
                }
            }

            result.Sort((a, b) => a.Order.CompareTo(b.Order));
            return result;
        }

        /// <summary>以 OrderOverride 值包装类型专属拦截器</summary>
        private sealed class TypedOrderedInterceptorWrapper<TData> : INavigationInterceptor<TData>
            where TData : struct
        {
            private readonly INavigationInterceptor<TData> _inner;
            public int Order { get; }

            public TypedOrderedInterceptorWrapper(INavigationInterceptor<TData> inner, int order)
            {
                _inner = inner;
                Order = order;
            }

            public System.Collections.IEnumerator OnNavigate(NavigationContext<TData> context)
            {
                return _inner.OnNavigate(context);
            }
        }
    }
}
