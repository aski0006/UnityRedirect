// ============================================================
// RouteNavigator Samples — PageView 页面路由目标
// ============================================================
// 挂载在页面 GameObject 上，实现 IRouteTarget<PageRouteData> 接口。
// 路由到达时自动调用 OnNavigateTo → StartPostProcess 协程。
//
// 使用：
//   1. 在 RouteDefinition 中将此 GameObject 设为 DirectReference，
//      或将其 Prefab 设为 PrefabReference。
//   2. 调用 RouteNavigator<PageRouteData>.Navigate("YourPageId", data);
// ============================================================

using System.Collections;
using UnityEngine;

namespace Kogane.RouteNavigator.Samples
{
    /// <summary>
    /// 页面视图组件。
    /// 实现 IRouteTarget&lt;PageRouteData&gt;，
    /// 路由到达时自动调用 OnNavigateTo → StartPostProcess。
    /// </summary>
    public sealed class PageView : MonoBehaviour, IRouteTarget<PageRouteData>
    {
        [Header("页面标识")]
        [Tooltip("与此页面关联的路由 ID，用于日志和调试")]
        [SerializeField] private string _routeId;

        [Header("UI 引用（示例）")]
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private GameObject _contentRoot;

        /// <summary>
        /// [同步] 路由到达后立即调用。
        /// 应保持轻量快速：SetActive、显示加载状态、初始化标题等。
        /// </summary>
        public void OnNavigateTo(PageRouteData data)
        {
            Debug.Log(
                $"[Samples] PageView.OnNavigateTo — " +
                $"RouteId: {_routeId}, FromPage: {data.FromPage}, Extra: {data.ExtraJson}");

            // 显示加载指示器（后处理完成后会隐藏）
            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(true);
            }

            // 确保页面自身处于激活状态
            gameObject.SetActive(true);
        }

        /// <summary>
        /// [协程] 启动异步后处理。
        /// 用于数据加载、网络请求、动画播放等耗时操作。
        /// 目标自身作为 MonoBehaviour 启动协程，当目标被 Destroy 时协程自动停止。
        ///
        /// 注意：此方法由 TargetResolverInterceptor 的 PostProcessWrapper 调用，
        /// 如果新导航在加载过程中被发起，PostProcessWrapper 会提前终止以节省资源。
        /// </summary>
        public void StartPostProcess(PageRouteData data)
        {
            // 使用 StartCoroutine 在当前 GameObject 上启动协程
            StartCoroutine(PostProcessRoutine(data));
        }

        /// <summary>
        /// 后处理协程。
        /// 模拟异步数据加载流程。
        /// </summary>
        private IEnumerator PostProcessRoutine(PageRouteData data)
        {
            // ── 阶段 1：模拟网络请求 ──
            Debug.Log($"[Samples] PageView 正在加载数据... (模拟 1 秒)");
            yield return new WaitForSeconds(1f);

            // ── 阶段 2：数据加载完成，隐藏加载指示器 ──
            if (_loadingIndicator != null)
            {
                _loadingIndicator.SetActive(false);
            }

            if (_contentRoot != null)
            {
                _contentRoot.SetActive(true);
            }

            // ── 阶段 3：播放入场动画（如果有） ──
            // 这里可以接入 DOTween、Animation 等动画系统
            // transform.DOScale(Vector3.one, 0.3f).From(Vector3.zero);

            Debug.Log($"[Samples] PageView 后处理完成: {_routeId}");

            // 从来源页面隐藏（如果来源不是自己）
            var fromPage = FindPageByRouteId(data.FromPage);
            if (fromPage != null && fromPage != this)
            {
                fromPage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 辅助方法：根据 routeId 查找页面。
        /// 实际项目中建议用注册表或依赖注入替代 FindObjectsOfType。
        /// </summary>
        private static PageView FindPageByRouteId(string routeId)
        {
            if (string.IsNullOrEmpty(routeId)) return null;

            var allPages = FindObjectsOfType<PageView>();
            foreach (var page in allPages)
            {
                if (page._routeId == routeId)
                    return page;
            }
            return null;
        }
    }
}
