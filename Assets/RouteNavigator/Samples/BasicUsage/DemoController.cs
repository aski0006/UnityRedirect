// ============================================================
// RouteNavigator Samples — DemoController 导航触发示例
// ============================================================
// 挂载到任意场景物体上，提供测试按钮触发导航。
//
// 使用方法：
//   1. 挂载到 Canvas 下的 GameObject。
//   2. 在 Inspector 中绑定 UI 按钮的事件，或直接调用公共方法。
//   3. 按数字键 1-5 可快速测试不同导航场景。
// ============================================================

using UnityEngine;

namespace Kogane.RouteNavigator.Samples
{
    /// <summary>
    /// 导航演示控制器。
    /// 提供 7 种导航调用示例，可直接绑定到 UI Button.onClick。
    /// </summary>
    public sealed class DemoController : MonoBehaviour
    {
        [Header("演示设置")]
        [Tooltip("按下数字键 1-5 触发不同导航演示")]
        [SerializeField] private bool _enableKeyboardShortcuts = true;

        [Header("路由 ID（需与 RouteDefinition 中的 routeId 匹配）")]
        [SerializeField] private string _mainMenuRouteId = "MainMenu";
        [SerializeField] private string _settingsRouteId = "Settings";
        [SerializeField] private string _shopRouteId = "Shop";
        [SerializeField] private string _confirmDialogRouteId = "ConfirmDialog";

        private void Update()
        {
            if (!_enableKeyboardShortcuts) return;

            // ── 键盘快捷键演示 ──
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                NavigateToMainMenu();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                NavigateToSettings();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                NavigateToShop();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                NavigateToShopWithItem("item_sword_001");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ShowConfirmDialog();
            }
        }

        // ----------------------------------------------------------
        // 公共导航方法（可直接绑定到 UI Button.onClick）
        // ----------------------------------------------------------

        /// <summary>
        /// 示例 1：简单导航到主菜单（带完成回调）。
        /// </summary>
        public void NavigateToMainMenu()
        {
            RouteNavigator<PageRouteData>.Navigate(
                routeId: _mainMenuRouteId,
                data: new PageRouteData
                {
                    PageId = _mainMenuRouteId,
                    FromPage = GetCurrentPageId(),
                    ExtraJson = null,
                },
                onComplete: result =>
                {
                    if (result.Success)
                        Debug.Log($"[Samples] 成功导航到主菜单");
                    else
                        Debug.LogError($"[Samples] 导航失败: {result.Message}");
                });
        }

        /// <summary>
        /// 示例 2：导航到设置页（无回调）。
        /// </summary>
        public void NavigateToSettings()
        {
            RouteNavigator<PageRouteData>.Navigate(
                routeId: _settingsRouteId,
                data: new PageRouteData
                {
                    PageId = _settingsRouteId,
                    FromPage = GetCurrentPageId(),
                });
        }

        /// <summary>
        /// 示例 3：导航到商店。
        /// </summary>
        public void NavigateToShop()
        {
            RouteNavigator<PageRouteData>.Navigate(
                routeId: _shopRouteId,
                data: new PageRouteData
                {
                    PageId = _shopRouteId,
                    FromPage = GetCurrentPageId(),
                });
        }

        /// <summary>
        /// 示例 4：导航到商店并携带额外数据（跳转到指定商品）。
        /// ExtraJson 可用于传递任意 JSON 字符串。
        /// </summary>
        public void NavigateToShopWithItem(string itemId)
        {
            RouteNavigator<PageRouteData>.Navigate(
                routeId: _shopRouteId,
                data: new PageRouteData
                {
                    PageId = _shopRouteId,
                    FromPage = GetCurrentPageId(),
                    ExtraJson = $"{{\"focusItem\": \"{itemId}\"}}",
                },
                onComplete: result =>
                {
                    // 可以在这里触发后续逻辑
                    // 例如：如果导航成功，自动打开购买面板
                });
        }

        /// <summary>
        /// 示例 5：弹出确认对话框。
        /// 使用 DialogRouteData 类型导航到确认弹窗。
        /// </summary>
        public void ShowConfirmDialog()
        {
            RouteNavigator<DialogRouteData>.Navigate(
                routeId: _confirmDialogRouteId,
                data: new DialogRouteData
                {
                    Title = "删除确认",
                    Message = "确定要删除该道具吗？此操作不可撤销。",
                    ConfirmText = "删除",
                    CancelText = "取消",
                    OnConfirmAction = "DeleteItem",
                    ConfirmArg = "item_sword_001",
                });
        }

        /// <summary>
        /// 示例 6：快速连续导航（模拟用户快速点击）。
        /// 只有最后一次导航会生效，之前的会被自动取消。
        /// 原理：RouteNavigator 版本号机制 — 新导航发起后旧协程自动终止。
        /// </summary>
        public void RapidNavigationTest()
        {
            // 快速触发三次导航，只有最后一次会完成
            RouteNavigator<PageRouteData>.Navigate(_mainMenuRouteId,
                new PageRouteData { PageId = _mainMenuRouteId });

            RouteNavigator<PageRouteData>.Navigate(_shopRouteId,
                new PageRouteData { PageId = _shopRouteId });

            RouteNavigator<PageRouteData>.Navigate(_settingsRouteId,
                new PageRouteData { PageId = _settingsRouteId });

            // 最终会导航到 Settings — 版本号机制确保旧导航协程被自动取消
            Debug.Log("[Samples] 快速导航测试：最终应到达 Settings");
        }

        /// <summary>
        /// 示例 7：使用空路由数据导航（无需参数的简单跳转）。
        /// </summary>
        public void NavigateWithEmptyData(string routeId)
        {
            RouteNavigator<EmptyRouteData>.Navigate(routeId, default);
        }

        // ----------------------------------------------------------
        // 辅助方法
        // ----------------------------------------------------------

        /// <summary>
        /// 获取当前活跃的页面 ID。
        /// 实际项目中可以维护一个导航栈或从全局状态获取。
        /// </summary>
        private static string GetCurrentPageId()
        {
            // 简化实现：查找所有 PageView 中处于激活状态的
            var allPages = FindObjectsOfType<PageView>();
            foreach (var page in allPages)
            {
                if (page.gameObject.activeInHierarchy)
                    return page.name;
            }
            return "Unknown";
        }

#if UNITY_EDITOR
        // 在 Inspector 中提供默认值
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_mainMenuRouteId))
                _mainMenuRouteId = "MainMenu";
            if (string.IsNullOrEmpty(_settingsRouteId))
                _settingsRouteId = "Settings";
            if (string.IsNullOrEmpty(_shopRouteId))
                _shopRouteId = "Shop";
            if (string.IsNullOrEmpty(_confirmDialogRouteId))
                _confirmDialogRouteId = "ConfirmDialog";
        }
#endif
    }
}
