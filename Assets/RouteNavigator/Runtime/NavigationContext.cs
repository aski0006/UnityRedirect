using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 泛型导航上下文。
    /// TData 作为 struct inline 字段存储在 class 堆内存中，零装箱。
    /// </summary>
    public sealed class NavigationContext<TData> : NavigationContextBase
        where TData : struct
    {
        private TData _data;

        /// <summary>强类型路由参数，直接访问无拆箱。</summary>
        public new TData Data
        {
            get => _data;
            set
            {
                _data = value;
                RawData = JsonUtility.ToJson(value);
            }
        }

        public NavigationContext(
            string routeId,
            TData data,
            GameObject source,
            int version
        )
        {
            RouteId = routeId;
            _data = data;
            Source = source;
            Version = version;
            Cancel = false;
            RawData = JsonUtility.ToJson(data);
            Result = new NavigationResult();
        }
    }
}
