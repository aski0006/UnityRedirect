using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 导航结果。记录一次导航的最终状态。
    /// </summary>
    public struct NavigationResult
    {
        /// <summary>导航是否成功</summary>
        public bool Success;

        /// <summary>目标 GameObject（成功时有效）</summary>
        public GameObject TargetObject;

        /// <summary>结果消息（失败原因或成功描述）</summary>
        public string Message;

        /// <summary>构造一个失败结果</summary>
        public static NavigationResult Failed(string message)
        {
            return new NavigationResult
            {
                Success = false,
                Message = message,
            };
        }

        /// <summary>构造一个取消结果</summary>
        public static NavigationResult Cancelled()
        {
            return new NavigationResult
            {
                Success = false,
                Message = "导航已取消",
            };
        }

        /// <summary>构造一个成功结果</summary>
        public static NavigationResult Succeeded(GameObject target)
        {
            return new NavigationResult
            {
                Success = true,
                TargetObject = target,
            };
        }
    }
}
