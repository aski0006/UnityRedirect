# Unity Route Navigator — 开发计划

> 版本：v7（最终定稿）
> 适用 Unity 版本：2022.3+
> 目标：开发一套开箱即用、图形化（UI Toolkit）、范式的 UI 路由导航系统

---

## 一、概述

### 1.1 设计理念

不替用户决定"路由后做什么"，而是提供一套**可组合的管道范式**——拦截器链任由用户编排，路由只负责解析目标并跑完整条管道。

### 1.2 核心能力

| 能力 | 说明 |
|------|------|
| 泛型路由参数 | `RouteNavigator<TData>` 泛型，`TData : struct`，零装箱 |
| 拦截器管道 | 通用拦截器 + 类型专属拦截器，Order 排序，可取消 |
| 目标解析 | 三种模式：Activate / Instantiate / LoadScene |
| 后处理异步 | `IRouteTarget<TData>` 同步激活 + 协程后处理 |
| 失败上浮 | 回调 `onComplete` + 全局事件 `OnNavigationResult` |
| 版本号机制 | 每次导航递增，后处理检查版本防止过期写入 |
| 热重载恢复 | 配置存于 ScriptableObject，域重载后自动恢复 |
| 编辑器工具 | UI Toolkit 单窗口三面板 + 类型选择器 + 路由图 |

---

## 二、系统架构

### 2.1 层次结构

```
┌──────────────────────────────────────────────────┐
│                 编辑器层 (Editor)                   │
│  RouteNavigatorWindow                             │
│  ├─ 类型选择器 (TypeDropdown)                      │
│  ├─ 路由列表面板 (RouteListPanel)                  │
│  ├─ 路由检视面板 (RouteInspectorPanel)             │
│  ├─ 管道面板 (PipelinePanel)                      │
│  └─ 路由图 (RouteGraphView)                       │
├──────────────────────────────────────────────────┤
│                 运行时层 (Runtime)                   │
│  RouteCore (非泛型核心)                            │
│  ├─ 路由定义表                                          │
│  ├─ 通用拦截器链                                    │
│  └─ 管道执行器                                     │
│                                                   │
│  RouteNavigator<TData> (泛型薄包装)                │
│  ├─ 类型专属拦截器链                                │
│  ├─ 版本号管理                                     │
│  └─ 结果分发                                       │
│                                                   │
│  IRouteTarget<TData> (目标组件接口)                 │
│  ├─ OnNavigateTo — 同步激活                        │
│  └─ StartPostProcess — 异步后处理                   │
├──────────────────────────────────────────────────┤
│                 存储层 (Serialization)              │
│  RouteDefinition — ScriptableObject 路由定义       │
│  RouteRegistry<TData> — 泛型路由注册表资产          │
│  InterceptorConfig — 拦截器序列化配置               │
└──────────────────────────────────────────────────┘
```

### 2.2 核心数据流

```
用户点击"前往任务"
  │
  ├─ 1. RouteNavigator<QuestRouteData>.Navigate("quest-detail", data, onComplete)
  │     ├─ 版本号递增
  │     ├─ 停止上一次导航协程
  │     └─ 构建 NavigationContext<QuestRouteData>(class, TData inline → 零装箱)
  │
  ├─ 2. ExecutePipeline 协程启动
  │
  ├─ 3. [通用拦截器] LoggingInterceptor       → 日志记录 (基类视图, RawData JSON)
  ├─ 4. [通用拦截器] RateLimitInterceptor     → 限流检查
  ├─ 5. [专属拦截器] QuestAccessInterceptor   → 权限检查 (强类型 ctx.Data 访问)
  │
  ├─ 6. [专属拦截器] TargetResolverInterceptor
  │     ├─ 查找 RouteDefinition "quest-detail"
  │     ├─ SceneManager.LoadSceneAsync / SetActive / Instantiate
  │     ├─ target.OnNavigateTo(data)           ← 同步轻量激活
  │     ├─ target.StartPostProcess(data)       ← 目标自身启动协程后处理
  │     └─ 写入 NavigationResult
  │
  ├─ 7. 管道结束
  │
  ├─ 8. onComplete?.Invoke(result)             ← 调用方拿到结果（成功/失败/原因）
  │     OnNavigationResult?.Invoke(args)       ← 全局事件（埋点/调试）
  │
  └─ 9. [目标自行管理] OnNavigatePostProcess 协程
        ├─ Network Request / IO / 动画
        ├─ 每次 yield 后检查版本号
        └─ 完成后更新 UI
```

---

## 三、类型与接口定义

### 3.1 路由数据标记

```csharp
// 标记一个 struct 可作为路由参数类型
// 编辑器通过此特性扫描可用类型
[AttributeUsage(AttributeTargets.Struct)]
public class RouteDataAttribute : Attribute { }
```

### 3.2 路由定义 (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Route Navigator/Route Definition")]
public class RouteDefinition : ScriptableObject
{
    [Header("基本信息")]
    public string routeId;
    public string displayName;

    [Header("目标类型")]
    public RouteTargetType targetType;   // Scene / GameObjectInScene / Prefab

    [Header("目标引用")]
    public SceneAsset sceneAsset;        // targetType = Scene 时使用
    public GameObject directReference;   // targetType = GameObjectInScene 时使用
    public GameObject prefabReference;   // targetType = Prefab 时使用

    [Header("加载行为")]
    public LoadMode loadMode;
    public bool unloadPrevious;

    [Header("标签与备注")]
    public string[] tags;
    public string description;
}

public enum RouteTargetType
{
    Scene,
    GameObjectInScene,
    Prefab
}

public enum LoadMode
{
    Activate,      // SetActive(true)
    Instantiate,   // Instantiate(prefab)
    LoadScene      // SceneManager.LoadSceneAsync
}
```

### 3.3 导航上下文

```csharp
// ── 非泛型基类（通用拦截器看到的视图）──
public class NavigationContextBase
{
    public string RouteId { get; protected set; }
    public bool Cancel { get; set; }
    public NavigationResult Result { get; set; }
    public string RawData { get; protected set; }  // JSON 只读视图
    public GameObject Source { get; protected set; }
}

// ── 泛型上下文（TData inline → 零装箱）──
public class NavigationContext<TData> : NavigationContextBase where TData : struct
{
    public new TData Data { get; set; }
    public int Version { get; internal set; }

    public NavigationContext(string routeId, TData data, int version)
    {
        RouteId = routeId;
        Data = data;
        Version = version;
        RawData = JsonUtility.ToJson(data);
        Result = new NavigationResult();
    }
}
```

### 3.4 导航结果

```csharp
public struct NavigationResult
{
    public bool Success;
    public GameObject TargetObject;
    public string Message;

    public static NavigationResult Failed(string message) =>
        new() { Success = false, Message = message };

    public static NavigationResult Cancelled() =>
        new() { Success = false, Message = "导航已取消" };

    public static NavigationResult Succeeded(GameObject target) =>
        new() { Success = true, TargetObject = target };
}
```

### 3.5 事件参数

```csharp
public class NavigationResultEventArgs : EventArgs
{
    public string RouteId { get; init; }
    public object Data { get; init; }       // 装箱，仅埋点/调试使用
    public NavigationResult Result { get; init; }
    public long TimestampMs { get; init; }
}
```

### 3.6 拦截器接口

```csharp
// ── 非泛型基接口（通用拦截器标记）──
public interface INavigationInterceptor
{
    int Order { get; }
}

// ── 非泛型拦截器（通用拦截器，访问基类视图）──
public interface INavigationInterceptorBase : INavigationInterceptor
{
    IEnumerator OnNavigate(NavigationContextBase context);
}

// ── 泛型拦截器（类型专属，强类型访问 TData）──
public interface INavigationInterceptor<TData> : INavigationInterceptor where TData : struct
{
    IEnumerator OnNavigate(NavigationContext<TData> context);
}
```

### 3.7 目标接口

```csharp
// ── 非泛型标记接口（用于类型判断）──
public interface IRouteTarget { }

// ── 泛型目标接口 ──
public interface IRouteTarget<TData> : IRouteTarget where TData : struct
{
    /// <summary>同步激活（轻量，不阻塞管道）</summary>
    void OnNavigateTo(TData data);

    /// <summary>启动异步后处理，目标自身作为 MonoBehaviour 启动协程</summary>
    void StartPostProcess(TData data);
}
```

### 3.8 RouteCore（非泛型核心）

```csharp
// 单例 ScriptableObject，非泛型核心
// 管理路由定义表和通用拦截器
public class RouteCore : ScriptableObject
{
    private static RouteCore _instance;
    public static RouteCore Instance => ...;

    [SerializeField] private List<RouteDefinition> routes;
    [SerializeField] private List<InterceptorConfig> globalInterceptorConfigs;

    private List<INavigationInterceptorBase> _globalInterceptors;

    public RouteDefinition GetRoute(string routeId) { ... }
    public T GetGlobalInterceptor<T>() where T : INavigationInterceptorBase { ... }

    // 热重载后从配置恢复
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() { ... }
}
```

### 3.9 RouteNavigator<TData>（泛型入口）

```csharp
public static class RouteNavigator<TData> where TData : struct
{
    private static int _currentVersion;
    private static Coroutine _currentCoroutine;
    private static MonoBehaviour _coroutineHost;
    private static List<INavigationInterceptor<TData>> _typedInterceptors;

    /// <summary>当前导航版本号，后处理通过对比此值判断是否过时</summary>
    public static int CurrentVersion => _currentVersion;

    /// <summary>全局导航结果事件（埋点/调试用）</summary>
    public static event Action<NavigationResultEventArgs> OnNavigationResult;

    static RouteNavigator()
    {
        // 从 ScriptableObject 加载持久化配置
        // 域重载后自动恢复
    }

    public static void Initialize(MonoBehaviour coroutineHost) { ... }

    public static void Navigate(string routeId, TData data,
        Action<NavigationResult> onComplete = null)
    { ... }

    public static void RegisterInterceptor(INavigationInterceptor<TData> interceptor) { ... }
    public static void RemoveInterceptor<T>() where T : INavigationInterceptor<TData> { ... }
    public static void Reset() { ... }  // 测试用

    private static IEnumerator ExecutePipeline(
        NavigationContext<TData> ctx, int version,
        Action<NavigationResult> onComplete)
    { ... }
}
```

---

## 四、内置拦截器

### 4.1 LoggingInterceptor（通用）

```csharp
public class LoggingInterceptor : INavigationInterceptorBase
{
    public int Order => -1000;
    public IEnumerator OnNavigate(NavigationContextBase ctx)
    {
        Debug.Log($"[Route] 导航: {ctx.RouteId}, Data={ctx.RawData}");
        yield break;
    }
}
```

### 4.2 TargetResolverInterceptor<TData>（类型专属）

```csharp
public class TargetResolverInterceptor<TData> : INavigationInterceptor<TData>
    where TData : struct
{
    public int Order => 500;

    public IEnumerator OnNavigate(NavigationContext<TData> ctx)
    {
        var def = RouteCore.Instance.GetRoute(ctx.RouteId);
        if (def == null)
        {
            ctx.Result = NavigationResult.Failed($"路由 \"{ctx.RouteId}\" 未找到");
            ctx.Cancel = true;
            yield break;
        }

        // 检查版本号（取消旧导航）
        var myVersion = ctx.Version;

        switch (def.loadMode)
        {
            case LoadMode.LoadScene:
                yield return LoadSceneAsync(def, ctx, myVersion);
                break;
            case LoadMode.Instantiate:
                yield return InstantiateTarget(def, ctx, myVersion);
                break;
            case LoadMode.Activate:
                yield return ActivateTarget(def, ctx, myVersion);
                break;
        }

        if (ctx.Cancel) yield break;

        // 通知目标
        var target = ctx.Result.TargetObject;
        if (target != null && target.TryGetComponent(out IRouteTarget<TData> routeTarget))
        {
            routeTarget.OnNavigateTo(ctx.Data);

            // 启动后处理协程（目标自身作为 MonoBehaviour 管理生命周期）
            if (routeTarget is MonoBehaviour mb)
            {
                mb.StartCoroutine(PostProcessWrapper(routeTarget, ctx.Data, ctx.Version));
            }
        }
    }

    private static IEnumerator PostProcessWrapper(
        IRouteTarget<TData> target, TData data, int version)
    {
        yield return null;  // 延迟一帧让管道优先完成
        try { target.StartPostProcess(data); }
        catch (Exception e)
        {
            Debug.LogError($"[Route] 后处理异常 [{target.GetType().Name}]: {e.Message}");
        }
    }

    private IEnumerator LoadSceneAsync(RouteDefinition def,
        NavigationContext<TData> ctx, int version)
    {
        var path = AssetDatabase.GetAssetPath(def.sceneAsset);
        var op = SceneManager.LoadSceneAsync(path);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            if (_currentVersion != version)
            {
                op.allowSceneActivation = true;  // 让加载完成但不激活
                ctx.Cancel = true;
                ctx.Result = NavigationResult.Cancelled();
                yield break;
            }
            if (op.progress >= 0.9f) op.allowSceneActivation = true;
            yield return null;
        }

        var scene = SceneManager.GetSceneByPath(path);
        ctx.Result = NavigationResult.Succeeded(GetRootGameObjects(scene));
    }

    private IEnumerator InstantiateTarget(RouteDefinition def,
        NavigationContext<TData> ctx, int version)
    {
        var obj = Object.Instantiate(def.prefabReference);
        ctx.Result = NavigationResult.Succeeded(obj);
        yield break;
    }

    private IEnumerator ActivateTarget(RouteDefinition def,
        NavigationContext<TData> ctx, int version)
    {
        def.directReference.SetActive(true);
        ctx.Result = NavigationResult.Succeeded(def.directReference);
        yield break;
    }
}
```

### 4.3 ConditionInterceptor<TData>（类型专属，可选）

```csharp
public class ConditionInterceptor<TData> : INavigationInterceptor<TData>
    where TData : struct
{
    public int Order => 200;
    private readonly Func<TData, bool> _condition;
    private readonly string _failMessage;

    public ConditionInterceptor(Func<TData, bool> condition, string failMessage = "条件不满足")
    {
        _condition = condition;
        _failMessage = failMessage;
    }

    public IEnumerator OnNavigate(NavigationContext<TData> ctx)
    {
        if (!_condition(ctx.Data))
        {
            ctx.Cancel = true;
            ctx.Result = NavigationResult.Failed(_failMessage);
        }
        yield break;
    }
}
```

---

## 五、编辑器工具（UI Toolkit）

### 5.1 窗口布局

```
┌─────────────────────────────────────────────────────────┐
│ [Route Navigator]                                        │
├─────────────────────────────────────────────────────────┤
│ [Type Dropdown ▼] QuestRouteData                        │
├─────────────────────────────┬───────────────────────────┤
│  Route List                 │  Route Inspector           │
│ ┌─────────────────────────┐ │ ┌───────────────────────┐ │
│ │ 🔍 Search...           │ │ │ Route ID: quest-detail│ │
│ │                         │ │ │ Display Name: 任务详情 │ │
│ │ ● quest-detail     ★   │ │ │ Target: [Scene]       │ │
│ │ ● main-menu            │ │ │ Scene: Level03.asset  │ │
│ │ ● settings             │ │ │ Load: LoadScene       │ │
│ │ ● inventory            │ │ │ Tags: [main][quest]   │ │
│ │                         │ │ │ Desc: 任务目标面板    │ │
│ │ [+ Add Route]          │ │ │                       │ │
│ └─────────────────────────┘ │ └───────────────────────┘ │
├─────────────────────────────┴───────────────────────────┤
│  Pipeline Panel (QuestRouteData 的拦截器链)              │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ Order │ Interceptor              │ Enabled │ Remove │ │
│ │ -1000 │ LoggingInterceptor       │   ✓    │   ✗    │ │
│ │  200  │ ConditionInterceptor     │   ✓    │   ✗    │ │
│ │  500  │ TargetResolverInterceptor│   ✓    │   ▒    │ │
│ │  ─────┴──────────────────────────┴────────┴──────── │ │
│ │  [+ Add Interceptor]     [Save Configuration]       │ │
│ └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 5.2 核心文件

| 文件 | 职责 |
|------|------|
| `RouteNavigatorWindow.cs` | 主窗口入口，加载 UXML/USS，管理面板切换 |
| `RouteNavigatorWindow.uxml` | UI Toolkit 布局（三面板结构） |
| `RouteNavigatorWindow.uss` | 样式表 |
| `RouteListPanel.cs` | 路由列表：搜索、过滤、增删、排序 |
| `RouteInspectorPanel.cs` | 路由详情：编辑 RouteDefinition 字段 |
| `PipelinePanel.cs` | 拦截器链管理：增删、启用/禁用、Order 排序 |
| `TypeSelector.cs` | 扫描 `[RouteData]` 类型，构建下拉列表 |
| `RouteGraphView.cs` | 路由图可视化 |

### 5.3 类型选择器

```csharp
// 使用 TypeCache 高效扫描
public static class RouteDataTypes
{
    public static List<Type> GetRouteDataTypes()
    {
        return TypeCache.GetTypesWithAttribute<RouteDataAttribute>()
            .Where(t => t.IsValueType && !t.IsEnum && t.IsPublic)
            .ToList();
    }
}
```

### 5.4 序列化方案

```csharp
// 泛型 ScriptableObject 子类，每个 TData 类型对应一个资产
// 路径: Assets/RouteNavigator/Registries/RouteRegistry_{TypeName}.asset
public class RouteRegistry<TData> : ScriptableObject where TData : struct
{
    public List<RouteDefinition> routes = new();
    public List<InterceptorConfig> typedInterceptors = new();
}

[Serializable]
public class InterceptorConfig
{
    public string typeName;      // 拦截器完整类型名
    public bool enabled = true;
    public int orderOverride;    // 0 = 使用代码中的 Order 值
}
```

---

## 六、目录结构

```
Assets/
└── RouteNavigator/
    ├── Runtime/
    │   ├── RouteDataAttribute.cs
    │   ├── RouteDefinition.cs
    │   ├── RouteCore.cs
    │   ├── RouteNavigator.cs
    │   ├── NavigationContextBase.cs
    │   ├── NavigationContext.cs
    │   ├── NavigationResult.cs
    │   ├── NavigationResultEventArgs.cs
    │   ├── IRouteTarget.cs
    │   ├── INavigationInterceptor.cs
    │   ├── PipelineEntry.cs
    │   └── Interceptors/
    │       ├── LoggingInterceptor.cs
    │       ├── TargetResolverInterceptor.cs
    │       └── ConditionInterceptor.cs
    ├── Editor/
    │   ├── RouteNavigatorWindow.cs
    │   ├── RouteNavigatorWindow.uxml
    │   ├── RouteNavigatorWindow.uss
    │   ├── RouteListPanel.cs
    │   ├── RouteInspectorPanel.cs
    │   ├── PipelinePanel.cs
    │   ├── TypeSelector.cs
    │   └── RouteGraphView.cs
    └── Samples/
        └── BasicUsage/
            ├── SampleRouteData.cs            — [RouteData] struct 示例
            ├── SampleQuestPanel.cs           — 发起导航示例
            └── SampleQuestDestination.cs     — IRouteTarget 示例
```

---

## 七、开发阶段与任务分配

### Phase 1：运行时核心（7 个文件）

| # | 文件 | 内容 | 预估 |
|---|------|------|------|
| 1 | `RouteDataAttribute.cs` | `[RouteData]` 特性标记 | 0.5h |
| 2 | `RouteDefinition.cs` | ScriptableObject + 枚举 | 1h |
| 3 | `NavigationResult.cs` | struct + 工厂方法 | 0.5h |
| 4 | `NavigationContextBase.cs` + `NavigationContext.cs` | 基类 + 泛型子类 | 1h |
| 5 | `NavigationResultEventArgs.cs` | 事件参数 | 0.5h |
| 6 | `IRouteTarget.cs` + `INavigationInterceptor.cs` | 接口定义 | 1h |
| 7 | `RouteCore.cs` + `RouteNavigator.cs` | 核心逻辑 + 管道执行器 | 3h |
| 8 | `LoggingInterceptor.cs` + `TargetResolverInterceptor.cs` + `ConditionInterceptor.cs` | 内置拦截器 | 2h |

### Phase 2：编辑器工具（6 个文件）

| # | 文件 | 内容 | 预估 |
|---|------|------|------|
| 9 | `TypeSelector.cs` | 类型扫描 + Dropdown | 1h |
| 10 | `RouteNavigatorWindow.uxml` + `.uss` | UI 布局和样式 | 2h |
| 11 | `RouteNavigatorWindow.cs` | 主窗口入口 | 1h |
| 12 | `RouteListPanel.cs` | 路由列表面板 | 2h |
| 13 | `RouteInspectorPanel.cs` | 路由详情面板 | 2h |
| 14 | `PipelinePanel.cs` | 拦截器链面板 | 2h |
| 15 | `RouteGraphView.cs` | 路由图可视化 | 1.5h |

### Phase 3：示例与文档

| # | 文件 | 内容 | 预估 |
|---|------|------|------|
| 16 | `SampleRouteData.cs` | 示例 struct | 0.5h |
| 17 | `SampleQuestPanel.cs` | 发起导航组件 | 1h |
| 18 | `SampleQuestDestination.cs` | 目标组件 | 1h |

---

## 八、测试计划

### 8.1 单元测试

| 测试用例 | 验证内容 |
|----------|----------|
| `NavigationContext_Cancel_Propagates` | 通用拦截器设置 Cancel=true → 管道中断 |
| `NavigationContext_Result_Accumulates` | 拦截器写入 Result → 后续拦截器可读 |
| `Interceptor_Order_Execution` | 按 Order 升序执行 |
| `Interceptor_Exception_StopsPipeline` | 拦截器异常 → Cancel=true → 中断 |
| `Version_Increment_OnNavigate` | 每次 Navigate 调用版本号递增 |
| `Version_Check_InTargetResolver` | 旧版本导航被跳过 |
| `RouteDefinition_NotFound` | 不存在的 routeId → Result.Failed |

### 8.2 集成测试

| 测试用例 | 验证内容 |
|----------|----------|
| `Navigate_WithOnComplete_Success` | 导航成功 → onComplete 收到 Success=true |
| `Navigate_WithOnComplete_Failed` | 导航失败 → onComplete 收到 Success=false + Message |
| `GlobalEvent_OnNavigationResult` | 导航完成 → 全局事件触发 |
| `RegisterInterceptor_ChainWorks` | 注册拦截器 → 管道中包含该拦截器 |
| `Reset_ClearsState` | Reset() → 拦截器列表和版本号重置 |

### 8.3 编辑器测试

| 测试用例 | 验证内容 |
|----------|----------|
| `Window_Opens_WithoutError` | 窗口打开不报错 |
| `TypeDropdown_ListsRouteDataTypes` | 下拉列出标记了 `[RouteData]` 的类型 |
| `AddRoute_CreatesEntry` | 新建路由 → 列表中出现 |
| `EditRoute_UpdatesFields` | 编辑路由字段 → 资产保存 |
| `DeleteRoute_RemovesEntry` | 删除路由 → 列表和资产移除 |

---

## 九、开发规范

依据 [git-standards](C:\Users\bronya\.codex\skills\git-standards\SKILL.md)：

### 提交规范

```bash
# Phase 1 提交
feat(runtime): 添加路由数据和上下文类型
feat(runtime): 实现泛型导航器和管道执行器
feat(runtime): 添加内置拦截器（日志、目标解析、条件）

# Phase 2 提交
feat(editor): 搭建 UI Toolkit 主窗口布局
feat(editor): 实现路由列表和检视面板
feat(editor): 实现拦截器链管理面板

# Phase 3 提交
feat(samples): 添加 BasicUsage 示例场景
docs: 补充使用文档和注释
```

### 代码风格

- 遵循 Unity C# 命名规范（PascalCase 公开成员，camelCase 私有字段）
- 所有公开 API 必须有 XML 注释
- 拦截器 Order 使用常量（`OrderFirst = -1000`，`OrderDefault = 0`，`OrderLast = 1000`）
- 使用 `nameof()` 代替字符串字面量

---

## 十、注意事项

### 10.1 热重载

- `RouteNavigator<TData>` 的静态构造器从 ScriptableObject 加载配置
- 静态字段在域重载后会丢失，但 ScriptableObject 资产保留
- `RouteCore` 使用 `[RuntimeInitializeOnLoadMethod]` 确保初始化

### 10.2 性能

- `NavigationContext<TData>` 是 class（单次导航分配一次），`TData` 是 struct inline 字段 → 零装箱
- 通用拦截器通过 `NavigationContextBase` 访问 `RawData`（JSON），只读不装箱
- 版本号检查是 int 比较，零开销

### 10.3 协程安全

- 协程宿主使用 `DontDestroyOnLoad` 常驻对象，跨场景不丢失
- 每次 `Navigate` 调用停止上一次协程
- 后处理协程绑定到目标 GameObject，目标销毁时自动停止
- 每次 `yield` 后推荐检查版本号

### 10.4 取消约束

```
⚠️ 导航取消的重要约束：

1. LoadScene 取消：已加载的场景资源会完成加载但不激活，最终被后续场景覆盖。

2. Instantiate 取消：已实例化的 Prefab 不会被自动删除。
   建议目标组件自行检查版本号判断是否过期。

3. Cancel 不回滚：设置 Cancel = true 只会停止后续拦截器执行，
   不会回滚已发生的副作用（Scene 加载、Prefab 实例化等）。
```
