# Route Navigator

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

**A type-safe, interceptor-pipeline based navigation system for Unity.**

Route Navigator 是一个基于**拦截器管道（Interceptor Pipeline）** 的类型安全 Unity 导航系统。通过 `ScriptableObject` 定义路由，通过协程式拦截器链处理导航逻辑，支持场景加载、Prefab 实例化、GameObject 激活三种目标模式。

---

## Table of Contents / 目录

- [Architecture / 架构](#architecture--架构)
- [Quick Start / 快速开始](#quick-start--快速开始)
- [Core Concepts / 核心概念](#core-concepts--核心概念)
- [API Reference / API 参考](#api-reference--api-参考)
- [Built-in Interceptors / 内置拦截器](#built-in-interceptors--内置拦截器)
- [Custom Interceptor / 自定义拦截器](#custom-interceptor--自定义拦截器)
- [Editor Tools / 编辑器工具](#editor-tools--编辑器工具)
- [Project Structure / 项目结构](#project-structure--项目结构)
- [Requirements / 环境要求](#requirements--环境要求)

---

## Architecture / 架构

```
Navigate("Shop", data)
        │
        ▼
┌─────────────────────────────────────────────┐
│         Interceptor Pipeline                │
│  (按 Order 从小到大依次执行)                  │
│                                             │
│  ┌─ LoggingInterceptor     (Order: -1000) ─┐│
│  ├─ LoadingScreenInterceptor (Order: -999) ─┤│  ← 自定义
│  ├─ ConditionInterceptor   (Order: 200)   ─┤│
│  ├─ TargetResolverInterceptor (Order: 500) ─┤│  ← 核心
│  └─ ...更多拦截器...                          ││
└─────────────────────────────────────────────┘
        │
        ▼
┌──────────────┐    ┌──────────────────┐
│ RouteTarget  │───▶│ OnNavigateTo()   │  同步回调
│ (GameObject) │    │ StartPostProcess()│  协程后处理
└──────────────┘    └──────────────────┘
```

**Pipeline rules / 管道规则：**
- 拦截器按 `Order` 从小到大依次执行
- 任何拦截器可设置 `context.Cancel = true` 中断管道
- 每次 `Navigate()` 调用产生新版本号，旧协程自动终止
- 拦截器异常被安全捕获，不会导致管道崩溃

---

## Quick Start / 快速开始

### 1. Initialize / 初始化

在启动场景的常驻 GameObject 上挂载初始化脚本：

```csharp
using Kogane.RouteNavigator;

public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // 以自身作为协程宿主初始化导航器
        RouteNavigator<PageRouteData>.Initialize(this);
    }
}
```

### 2. Define Routes / 定义路由

通过 `Window > Route Navigator` 打开编辑器窗口，创建 `RouteDefinition` 资产：

| 字段 | 说明 |
|------|------|
| Route Id | 路由唯一标识（如 `"MainMenu"`, `"Shop"`） |
| Target Type | `Scene` / `GameObjectInScene` / `Prefab` |
| Load Mode | `LoadScene` / `Instantiate` / `Activate` |

### 3. Implement Route Target / 实现路由目标

```csharp
using Kogane.RouteNavigator;

public class ShopPage : MonoBehaviour, IRouteTarget<PageRouteData>
{
    // 路由到达时立即调用（同步，轻量）
    public void OnNavigateTo(PageRouteData data)
    {
        gameObject.SetActive(true);
    }

    // 异步后处理（协程，可用于网络请求、动画等）
    public void StartPostProcess(PageRouteData data)
    {
        StartCoroutine(LoadShopData(data));
    }

    IEnumerator LoadShopData(PageRouteData data)
    {
        yield return new WaitForSeconds(0.5f);
        // 加载商店数据...
    }
}
```

### 4. Navigate / 发起导航

```csharp
// 简单导航
RouteNavigator<PageRouteData>.Navigate("Shop",
    new PageRouteData { PageId = "Shop", FromPage = "MainMenu" });

// 带完成回调
RouteNavigator<PageRouteData>.Navigate("Shop",
    new PageRouteData { PageId = "Shop" },
    result =>
    {
        if (result.Success)
            Debug.Log($"到达: {result.TargetObject.name}");
    });
```

---

## Core Concepts / 核心概念

### Route Data Struct / 路由数据结构

```csharp
[Serializable]
public struct PageRouteData
{
    public string PageId;      // 目标页面 ID
    public string FromPage;    // 来源页面 ID
    public string ExtraJson;   // 扩展数据（JSON）
}
```

- 必须是值类型 `struct`
- 实现 `IRouteTarget<TData>` 的目标自动接收该类型参数
- 零装箱：`TData` 作为 `class` 的 inline 字段存储

### Route Target / 路由目标

```csharp
public interface IRouteTarget<TData> : IRouteTarget
    where TData : struct
{
    void OnNavigateTo(TData data);      // 同步回调，轻量快速
    void StartPostProcess(TData data);  // 协程后处理，耗时操作
}
```

### Interceptor / 拦截器

```csharp
// 类型专属拦截器（仅作用于指定 TData）
public interface INavigationInterceptor<TData> : INavigationInterceptor
    where TData : struct
{
    int Order { get; }
    IEnumerator OnNavigate(NavigationContext<TData> context);
}

// 通用拦截器（作用于所有 TData，只读访问）
public interface INavigationInterceptorBase : INavigationInterceptor
{
    IEnumerator OnNavigate(NavigationContextBase context);
}
```

---

## API Reference / API 参考

### RouteNavigator\<TData\>

| Method | Description |
|--------|-------------|
| `Initialize(MonoBehaviour host)` | 初始化导航器，设置协程宿主 |
| `Navigate(string routeId, TData data)` | 发起导航 |
| `Navigate(routeId, data, onComplete)` | 发起导航，带完成回调 |
| `RegisterInterceptor(interceptor)` | 动态注册类型专属拦截器 |
| `RemoveInterceptor<T>()` | 移除指定类型的拦截器 |
| `Reset()` | 重置导航器状态（测试用） |

### NavigationContext\<TData\>

| Property | Description |
|----------|-------------|
| `RouteId` | 当前路由标识 |
| `Data` | 强类型路由参数（零装箱） |
| `Cancel` | 设为 `true` 中断管道 |
| `Result` | 导航结果（拦截器可累积写入） |
| `Source` | 发起导航的源 GameObject |
| `Version` | 当前导航版本号 |

### NavigationResult

| Method | Description |
|--------|-------------|
| `Succeeded(GameObject target)` | 创建成功结果 |
| `Failed(string message)` | 创建失败结果 |
| `Cancelled()` | 创建取消结果 |

### InterceptorOrders

| Constant | Value | Purpose |
|----------|-------|---------|
| `First` | -1000 | 最先执行（日志、埋点） |
| `Default` | 0 | 默认顺序 |
| `Early` | 200 | 通用检查（权限、条件） |
| `ResolveTarget` | 500 | 目标解析 |
| `Last` | 1000 | 最后执行 |

### Events / 事件

```csharp
// 全局导航结果事件（埋点/调试用）
RouteNavigator<PageRouteData>.OnNavigationResult += args =>
{
    Debug.Log($"Route: {args.RouteId}, Success: {args.Result.Success}");
};
```

---

## Built-in Interceptors / 内置拦截器

| Interceptor | Type | Order | Description |
|-------------|------|-------|-------------|
| `LoggingInterceptor` | Base | -1000 | 记录所有导航的开始信息 |
| `ConditionInterceptor<TData>` | Typed | 200 | 根据路由参数检查条件 |
| `TargetResolverInterceptor<TData>` | Typed | 500 | 解析目标（场景加载/实例化/激活） |

### ConditionInterceptor Usage

```csharp
RouteNavigator<PageRouteData>.RegisterInterceptor(
    new ConditionInterceptor<PageRouteData>(
        condition: data => data.PageId != "AdminPanel",
        failMessage: "权限不足，无法访问该页面"
    ));
```

---

## Custom Interceptor / 自定义拦截器

### Sync Interceptor / 同步拦截器

```csharp
public sealed class PermissionCheckInterceptor<TData> : INavigationInterceptor<TData>
    where TData : struct
{
    public int Order => InterceptorOrders.Early;

    public IEnumerator OnNavigate(NavigationContext<TData> context)
    {
        if (!HasPermission(context.RouteId))
        {
            context.Cancel = true;
            context.Result = NavigationResult.Failed("无权限");
        }
        yield break;
    }
}
```

### Async Interceptor / 异步拦截器

```csharp
public sealed class LoadingScreenInterceptor<TData> : INavigationInterceptor<TData>
    where TData : struct
{
    public int Order => InterceptorOrders.First + 1;

    public IEnumerator OnNavigate(NavigationContext<TData> context)
    {
        ShowLoadingScreen();
        yield return new WaitForSeconds(0.5f);  // 最小显示时间
        // 不取消导航，让管道继续
    }
}
```

---

## Editor Tools / 编辑器工具

通过 `Window > Route Navigator` 打开管理窗口：

| Panel | Description |
|-------|-------------|
| Route List / 路由列表 | 管理所有 RouteDefinition 资产 |
| Inspector / 检查器 | 编辑选中路由的属性（目标、加载模式、标签、UnityEvent） |
| Pipeline / 管道 | 配置通用拦截器和各 TData 类型的专属拦截器 |

**Features / 功能：**
- 三栏可拖拽布局
- 拦截器启用/禁用/排序
- RouteDefinition 标签过滤与定位
- UnityEvent 编辑器配置
- 拦截器配置持久化（ScriptableObject）

---

## Project Structure / 项目结构

```
Assets/RouteNavigator/
├── Runtime/
│   ├── RouteCore.cs                  # 非泛型核心单例（路由表 + 通用拦截器）
│   ├── RouteNavigator.cs             # 泛型导航入口（TData 专属拦截器 + 管道）
│   ├── RouteDefinition.cs            # 路由定义 ScriptableObject
│   ├── RouteRegistry.cs              # 拦截器持久化注册表
│   ├── RouteDataAttribute.cs         # [RouteData] 标记特性
│   ├── NavigationContext.cs          # 泛型导航上下文
│   ├── NavigationContextBase.cs      # 非泛型上下文基类
│   ├── NavigationResult.cs           # 导航结果 struct
│   ├── NavigationResultEventArgs.cs  # 结果事件参数
│   ├── INavigationInterceptor.cs     # 拦截器接口 + InterceptorOrders
│   ├── IRouteTarget.cs               # 路由目标接口
│   └── Interceptors/
│       ├── LoggingInterceptor.cs     # 通用日志拦截器
│       ├── ConditionInterceptor.cs   # 条件检查拦截器
│       └── TargetResolverInterceptor.cs  # 目标解析拦截器
├── Editor/
│   ├── RouteNavigatorWindow.cs       # 主编辑器窗口
│   ├── RouteNavigatorAssets.cs       # 资产自动初始化
│   ├── RouteListPanel.cs             # 路由列表面板
│   ├── RouteInspectorPanel.cs        # 检查器面板
│   ├── PipelinePanel.cs              # 管道配置面板
│   └── TypeSelector.cs               # 类型下拉选择器
└── Samples/
    └── BasicUsage/
        ├── RouteDataStructs.cs       # 5 种示例路由数据 struct
        ├── BasicUsageExample.cs      # RouteBootstrapper + LoadingScreenInterceptor
        ├── PageView.cs               # IRouteTarget<PageRouteData> 页面目标
        ├── DialogController.cs       # IRouteTarget<DialogRouteData> 弹窗目标
        └── DemoController.cs         # 7 种导航调用示例
```

---

## Design Decisions / 设计决策

| Decision | Rationale |
|----------|-----------|
| `TData : struct` 约束 | 值类型避免堆分配，零装箱 |
| 协程管道而非 async/await | Unity 协程与 MonoBehaviour 生命周期集成更好 |
| 非泛型 RouteCore + 泛型 RouteNavigator | 分离通用逻辑与类型专属逻辑 |
| 版本号机制 | 新导航自动取消旧导航，无需手动管理 |
| 单一 RouteRegistry（非每 TData 一个） | 减少资产数量，编辑器可在单窗口中管理 |

---

## Requirements / 环境要求

- Unity 2021.3 or later
- No external dependencies / 无外部依赖

---

## License

MIT

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
