// ============================================================
// RouteNavigator Samples — DialogController 对话框路由目标
// ============================================================
// 实现 IRouteTarget<DialogRouteData>，展示带有确认/取消操作的模态弹窗。
// 路由到达时自动调用 OnNavigateTo → StartPostProcess（弹出动画）。
//
// 使用：
//   RouteNavigator<DialogRouteData>.Navigate("ConfirmDialog",
//       new DialogRouteData { Title = "确认", Message = "...", ... });
// ============================================================

using System.Collections;
using UnityEngine;

namespace Kogane.RouteNavigator.Samples
{
    /// <summary>
    /// 对话框控制器。
    /// 实现 IRouteTarget&lt;DialogRouteData&gt;，
    /// 展示带有确认 / 取消操作的模态弹窗。
    ///
    /// 当前实现为轻量示例，通过 Debug.Log 输出操作结果。
    /// 实际项目中将 GameObject 字段改为 UnityEngine.UI.Text / UnityEngine.UI.Button 类型
    /// 即可获得完整的 Inspector 拖拽体验。
    /// </summary>
    public sealed class DialogController : MonoBehaviour, IRouteTarget<DialogRouteData>
    {
        [Header("UI 根节点")]
        [Tooltip("对话框根 GameObject，通过 SetActive 控制显示/隐藏")]
        [SerializeField] private GameObject _dialogRoot;

        [Header("UI 文本（实际项目替换为 UnityEngine.UI.Text）")]
        [SerializeField] private GameObject _titleLabel;
        [SerializeField] private GameObject _messageLabel;

        [Header("UI 按钮（实际项目替换为 UnityEngine.UI.Button）")]
        [SerializeField] private GameObject _confirmButtonObj;
        [SerializeField] private GameObject _cancelButtonObj;

        private DialogRouteData _currentData;
        private bool _isProcessing;

        /// <summary>
        /// [同步] 路由到达，立即显示对话框 UI。
        /// </summary>
        public void OnNavigateTo(DialogRouteData data)
        {
            _currentData = data;
            _isProcessing = false;

            Debug.Log(
                $"[Samples] Dialog.OnNavigateTo — " +
                $"Title: {data.Title}, Message: {data.Message}");

            // 实际项目：通过 Text 组件设置文本
            // _titleText.text = data.Title ?? "提示";
            // _messageText.text = data.Message ?? string.Empty;
            // 当前示例：仅通过 GameObject 名称确认引用有效
            if (_titleLabel != null)
                _titleLabel.name = $"Title_{data.Title ?? "Dialog"}";
            if (_messageLabel != null)
                _messageLabel.name = $"Message_{data.Message ?? ""}";

            // 显示对话框
            if (_dialogRoot != null)
                _dialogRoot.SetActive(true);
            gameObject.SetActive(true);

            // 实际项目：通过 Button.onClick.AddListener 注册回调
            // _confirmButton.onClick.RemoveAllListeners();
            // _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        /// <summary>
        /// [协程] 启动后处理。
        /// 对话框的后处理通常较轻量（如播放弹出动画），不需要复杂逻辑。
        /// </summary>
        public void StartPostProcess(DialogRouteData data)
        {
            StartCoroutine(PostProcessRoutine());
        }

        private IEnumerator PostProcessRoutine()
        {
            // 播放弹出动画（示例使用简单的缩放动画）
            if (_dialogRoot != null)
            {
                var t = 0f;
                var startScale = Vector3.one * 0.5f;
                var endScale = Vector3.one;
                var duration = 0.3f;

                while (t < duration)
                {
                    t += Time.deltaTime;
                    var progress = Mathf.Clamp01(t / duration);
                    // 缓出效果
                    var eased = 1f - (1f - progress) * (1f - progress);
                    _dialogRoot.transform.localScale = Vector3.Lerp(startScale, endScale, eased);
                    yield return null;
                }
                _dialogRoot.transform.localScale = endScale;
            }
        }

        /// <summary>
        /// 确认按钮回调（实际项目中绑定到 Button.onClick）。
        /// </summary>
        public void OnConfirmClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            Debug.Log(
                $"[Samples] Dialog 确认 — Action: {_currentData.OnConfirmAction}, " +
                $"Arg: {_currentData.ConfirmArg}");

            // 实际项目中在这里调用业务逻辑
            // 例如：删除道具、购买商品、保存设置等

            Close();
        }

        /// <summary>
        /// 取消按钮回调（实际项目中绑定到 Button.onClick）。
        /// </summary>
        public void OnCancelClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            Debug.Log("[Samples] Dialog 取消");

            Close();
        }

        /// <summary>
        /// 关闭对话框。
        /// </summary>
        public void Close()
        {
            if (_dialogRoot != null)
                _dialogRoot.SetActive(false);

            _isProcessing = false;

            // 可选：导航回上一页或关闭自身
            // RouteNavigator<PageRouteData>.Navigate(
            //     _currentData.ReturnRoute ?? "MainMenu", default);
        }
    }
}
