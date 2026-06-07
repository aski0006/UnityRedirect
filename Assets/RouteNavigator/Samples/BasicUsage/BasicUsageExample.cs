// ============================================================
// RouteNavigator Samples — 完整使用示例
// ============================================================
// 使用方式（开箱即用）：
//   1. 在启动场景中创建一个空 GameObject，挂载 RouteBootstrapper。
//   2. 创建两个页面 GameObject（PageA / PageB），分别挂载 PageView。
//   3. 点击 Play，使用 DemoController 测试导航。
//
// 架构概览：
//   RouteBootstrapper  →  初始化 RouteNavigator + 注册拦截器
//   PageView           →  实现 IRouteTarget<TData> 的页面组件
//   DialogController   →  实现 IRouteTarget<TData> 的弹窗组件（含后处理）
//   *Interceptor       →  自定义拦截器（日志、权限、加载动画等）
//   DemoController     →  演示如何调用 Navigate
// ============================================================

using System.Collections;
using UnityEngine;

namespace Kogane.RouteNavigator.Samples {
    // ============================================================
    // 第一部分：RouteBootstrapper — 初始化与配置
    // ============================================================

    /// <summary>
    /// 路由引导器。
    /// 挂载到启动场景的常驻 GameObject 上，负责：
    /// 1. 初始化 RouteNavigator（设置协程宿主）
    /// 2. 注册自定义拦截器
    /// 3. 监听全局导航结果（埋点 / 调试）
    ///
    /// 使用：将此脚本挂载到场景中任意 GameObject，
    /// 建议放在第一个加载的场景中，并设为 DontDestroyOnLoad。
    /// </summary>
    public sealed class RouteBootstrapper : MonoBehaviour {
        [Header("初始化设置")]
        [Tooltip("是否在 Awake 时自动初始化")]
        [SerializeField] private bool _autoInit = true;

        [Tooltip("是否在该物体上注册全局导航结果监听")]
        [SerializeField] private bool _listenGlobalResult = true;

        private void Awake() {
            if (_autoInit) {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化路由系统。
        /// 可在代码中手动调用，也可通过 _autoInit 在 Awake 自动执行。
        /// </summary>
        public void Initialize() {
            // ── 步骤 1：以自身作为协程宿主初始化导航器 ──
            // RouteNavigator<TData> 是泛型静态类，每个 TData 类型有独立的拦截器链。
            // Initialize 只需对一种类型调用一次，它会将宿主 GameObject 设为 DontDestroyOnLoad。
            RouteNavigator<PageRouteData>.Initialize(this);

            // 其他 TData 类型共享同一个宿主，无需重复 Initialize。
            // 如果项目中有多种路由数据类型，只需确保第一种类型的 Initialize 已被调用。

            Debug.Log("[Samples] RouteNavigator 初始化完成");

            // ── 步骤 2：注册自定义拦截器（可选） ──
            RegisterInterceptors();

            // ── 步骤 3：监听全局导航结果（可选） ──
            if (_listenGlobalResult) {
                RouteNavigator<PageRouteData>.OnNavigationResult += OnNavigationResult;
            }
        }

        /// <summary>
        /// 注册自定义拦截器。
        /// 拦截器按 Order 字段从小到大依次执行。
        /// </summary>
        private static void RegisterInterceptors() {
            // 注册 LoadingScreenInterceptor（Order = InterceptorOrders.First + 1 = -999）
            // 它在日志拦截器之后、条件检查之前执行，显示加载动画
            RouteNavigator<PageRouteData>.RegisterInterceptor(
                new LoadingScreenInterceptor<PageRouteData>());

            // 注册权限检查拦截器（Order = InterceptorOrders.Early = 200）
            // 它在目标解析之前检查是否可以导航到目标页面
            RouteNavigator<PageRouteData>.RegisterInterceptor(
                new ConditionInterceptor<PageRouteData>(
                    condition: data => {
                        // 示例：禁止导航到 "AdminPanel" 页面（模拟权限不足）
                        if (data.PageId == "AdminPanel") {
                            Debug.LogWarning("[Samples] 权限不足：无法访问 AdminPanel");
                            return false;
                        }
                        return true;
                    },
                    failMessage: "权限不足，无法访问该页面"
                ));
        }

        /// <summary>
        /// 全局导航结果回调（埋点 / 调试用）。
        /// 每次导航完成（无论成功或失败）都会触发。
        /// </summary>
        private static void OnNavigationResult(NavigationResultEventArgs args) {
            Debug.Log(
                $"[Samples] 导航结果 — Route: {args.RouteId}, " +
                $"Success: {args.Result.Success}, " +
                $"Message: {args.Result.Message}, " +
                $"Target: {args.Result.TargetObject?.name ?? "null"}, " +
                $"Time: {args.TimestampMs}ms");
        }

        private void OnDestroy() {
            RouteNavigator<PageRouteData>.OnNavigationResult -= OnNavigationResult;
        }
    }

    // ============================================================
    // 第四部分：LoadingScreenInterceptor — 自定义异步拦截器
    // ============================================================

    /// <summary>
    /// 加载动画拦截器。
    /// 在目标解析前显示加载画面，加载完成后自动隐藏。
    /// 演示 "异步拦截器" 模式 — 使用 yield return 实现多帧等待。
    ///
    /// 注意：Unity 的协程支持 yield return null（等一帧）、
    /// yield return new WaitForSeconds(n)、yield return AsyncOperation 等。
    /// </summary>
    public sealed class LoadingScreenInterceptor<TData> : INavigationInterceptor<TData>
        where TData : struct {
        /// <summary>在日志拦截器之后、条件检查之前执行</summary>
        public int Order => InterceptorOrders.First + 1;

        public IEnumerator OnNavigate(NavigationContext<TData> context) {
            Debug.Log($"[Samples] LoadingScreen 显示: {context.RouteId}");

            // ── 显示加载画面 ──
            // 实际项目中这里会激活一个全屏 UI Canvas
            // var loadingScreen = LoadingScreen.Instance;
            // if (loadingScreen != null) loadingScreen.Show($"正在加载 {context.RouteId}");

            // ── 等待至少 0.5 秒（避免闪烁） ──
            yield return new WaitForSeconds(0.5f);

            // ── 模拟异步加载资源 ──
            // var asyncOp = Resources.LoadAsync<GameObject>(prefabPath);
            // yield return asyncOp;

            // ── 拦截器不取消导航，让管道继续到 TargetResolverInterceptor ──
            // 注意：这里不做 context.Cancel = true，只是提供视觉效果

            Debug.Log($"[Samples] LoadingScreen 最小等待完成: {context.RouteId}");

            // ── 加载完成后，由 PageView.StartPostProcess 负责隐藏 ──
        }
    }
}
