// ============================================================
// RouteNavigator Samples — 路由数据结构定义
// ============================================================
// 本文件定义示例路由参数 struct，开箱即用。
// TData 必须是值类型 struct 以满足 RouteNavigator<TData> 的约束。
// ============================================================

using System;

namespace Kogane.RouteNavigator.Samples {
    // ----------------------------------------------------------
    // 示例 1：空路由参数
    // 适用于不需要额外参数的简单导航
    // ----------------------------------------------------------
    /// <summary>
    /// 空路由参数。适用于不需要传递额外数据的简单页面跳转。
    /// <code>
    ///   RouteNavigator&lt;EmptyRouteData&gt;.Navigate("MainMenu", default);
    /// </code>
    /// </summary>
    [Serializable]
    [RouteData]
    public struct EmptyRouteData {
        // 无字段 — 直接使用 default 即可
    }

    // ----------------------------------------------------------
    // 示例 2：通用页面路由参数
    // 适用于大多数页面跳转场景
    // ----------------------------------------------------------
    /// <summary>
    /// 通用页面路由参数。
    /// 支持携带来源页面信息，方便实现返回逻辑。
    /// <code>
    ///   RouteNavigator&lt;PageRouteData&gt;.Navigate("Shop",
    ///       new PageRouteData { PageId = "Shop", FromPage = "MainMenu" });
    /// </code>
    /// </summary>
    [Serializable]
    [RouteData]
    public struct PageRouteData {
        /// <summary>目标页面 ID</summary>
        public string PageId;

        /// <summary>来源页面 ID（可用于返回导航）</summary>
        public string FromPage;

        /// <summary>可选 JSON 扩展数据（如商品 ID、关卡编号等）</summary>
        public string ExtraJson;
    }

    // ----------------------------------------------------------
    // 示例 3：对话框路由参数
    // 适用于弹窗、确认框等模态 UI
    // ----------------------------------------------------------
    /// <summary>
    /// 对话框路由参数。适用于弹窗 / 模态 UI 的导航。
    /// <code>
    ///   RouteNavigator&lt;DialogRouteData&gt;.Navigate("ConfirmDialog",
    ///       new DialogRouteData
    ///       {
    ///           Title = "确认删除",
    ///           Message = "此操作不可撤销，确定继续？",
    ///           OnConfirmAction = "DeleteItem",
    ///           ConfirmArg = "item_001"
    ///       });
    /// </code>
    /// </summary>
    [Serializable]
    [RouteData]
    public struct DialogRouteData {
        /// <summary>对话框标题</summary>
        public string Title;

        /// <summary>对话框正文</summary>
        public string Message;

        /// <summary>确认按钮文字</summary>
        public string ConfirmText;

        /// <summary>取消按钮文字</summary>
        public string CancelText;

        /// <summary>确认回调的标识符（由目标自行解析）</summary>
        public string OnConfirmAction;

        /// <summary>确认回调的附加参数</summary>
        public string ConfirmArg;
    }

    // ----------------------------------------------------------
    // 示例 4：关卡 / 战斗路由参数
    // 适用于携带关卡配置的场景加载
    // ----------------------------------------------------------
    /// <summary>
    /// 关卡 / 战斗路由参数。
    /// 携带关卡配置 ID 和难度等上下文信息。
    /// <code>
    ///   RouteNavigator&lt;LevelRouteData&gt;.Navigate("BattleScene",
    ///       new LevelRouteData { LevelId = 42, Difficulty = 3, Seed = 12345 });
    /// </code>
    /// </summary>
    [Serializable]
    [RouteData]
    public struct LevelRouteData {
        /// <summary>关卡 ID</summary>
        public int LevelId;

        /// <summary>难度等级 (0=简单, 1=普通, 2=困难, 3=地狱)</summary>
        public int Difficulty;

        /// <summary>随机种子（0 表示随机）</summary>
        public int Seed;

        /// <summary>是否为练习模式</summary>
        public bool IsPractice;
    }

    // ----------------------------------------------------------
    // 示例 5：角色详情路由参数
    // 适用于展示角色 / 物品详情的场景
    // ----------------------------------------------------------
    /// <summary>
    /// 角色 / 物品详情路由参数。
    /// 携带目标实体 ID 和可选的返回操作。
    /// <code>
    ///   RouteNavigator&lt;DetailRouteData&gt;.Navigate("CharacterDetail",
    ///       new DetailRouteData
    ///       {
    ///           EntityId = "char_001",
    ///           EntityType = "Character",
    ///           CanEdit = true,
    ///           ReturnRoute = "CharacterList"
    ///       });
    /// </code>
    /// </summary>
    [Serializable]
    [RouteData]
    public struct DetailRouteData {
        /// <summary>实体唯一标识</summary>
        public string EntityId;

        /// <summary>实体类型（如 "Character", "Item", "Skill"）</summary>
        public string EntityType;

        /// <summary>是否允许编辑</summary>
        public bool CanEdit;

        /// <summary>返回时导航到的路由 ID</summary>
        public string ReturnRoute;
    }
}
