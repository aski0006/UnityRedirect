using System;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 标记一个 struct 可作为路由参数类型。
    /// 编辑器通过此特性扫描可用类型构建下拉列表。
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class RouteDataAttribute : Attribute
    {
    }
}
