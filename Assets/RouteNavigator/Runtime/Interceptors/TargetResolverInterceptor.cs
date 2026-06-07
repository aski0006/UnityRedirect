using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kogane.RouteNavigator
{
    public sealed class TargetResolverInterceptor<TData> : INavigationInterceptor<TData>
        where TData : struct
    {
        public int Order => InterceptorOrders.ResolveTarget;

        public IEnumerator OnNavigate(NavigationContext<TData> context)
        {
            var core = RouteCore.Instance;
            if (core == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed("RouteCore 未初始化");
                yield break;
            }

            var definition = core.GetRoute(context.RouteId);
            if (definition == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed(
                    $"路由 \"{context.RouteId}\" 未找到");
                yield break;
            }

            var myVersion = context.Version;

            switch (definition.LoadMode)
            {
                case LoadMode.LoadScene:
                    yield return LoadSceneAsync(definition, context, myVersion);
                    break;

                case LoadMode.Instantiate:
                    yield return InstantiateTarget(definition, context, myVersion);
                    break;

                case LoadMode.Activate:
                    yield return ActivateTarget(definition, context, myVersion);
                    break;

                default:
                    context.Cancel = true;
                    context.Result = NavigationResult.Failed(
                        $"不支持的 LoadMode: {definition.LoadMode}");
                    yield break;
            }

            if (context.Cancel)
            {
                yield break;
            }

            // ── 通知目标并启动后处理 ──
            var target = context.Result.TargetObject;
            if (target != null && target.TryGetComponent(out IRouteTarget<TData> routeTarget))
            {
                routeTarget.OnNavigateTo(context.Data);

                if (routeTarget is MonoBehaviour mb)
                {
                    mb.StartCoroutine(PostProcessWrapper(routeTarget, context.Data, myVersion));
                }
            }

            // ── 触发 RouteDefinition 上的 UnityEvent ──
            definition.OnNavigationComplete?.Invoke();
        }

        private static IEnumerator LoadSceneAsync(
            RouteDefinition definition,
            NavigationContext<TData> context,
            int version)
        {
            var scenePath = definition.ScenePath;
            if (string.IsNullOrEmpty(scenePath))
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed("场景路径为空");
                yield break;
            }

            var operation = SceneManager.LoadSceneAsync(scenePath);
            if (operation == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed(
                    $"场景加载失败: {scenePath}");
                yield break;
            }

            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                if (RouteNavigator<TData>.CurrentVersion != version)
                {
                    // 新导航已发起，放弃本次操作。
                    // 不设置 allowSceneActivation，让加载停留在 90% 但不激活。
                    // 该场景资源后续会被 GC 回收或被子场景加载覆盖。
                    context.Cancel = true;
                    context.Result = NavigationResult.Cancelled();
                    yield break;
                }

                if (operation.progress >= 0.9f)
                {
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed(
                    $"场景加载后无效: {scenePath}");
                yield break;
            }

            var rootObjects = scene.GetRootGameObjects();
            GameObject targetObject = null;
            if (rootObjects.Length > 0)
            {
                targetObject = rootObjects[0];
            }

            context.Result = NavigationResult.Succeeded(targetObject);
        }

        private static IEnumerator InstantiateTarget(
            RouteDefinition definition,
            NavigationContext<TData> context,
            int version)
        {
            var prefab = definition.PrefabReference;
            if (prefab == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed("Prefab 引用为空");
                yield break;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            if (instance == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed("Prefab 实例化失败");
                yield break;
            }

            context.Result = NavigationResult.Succeeded(instance);
            yield break;
        }

        private static IEnumerator ActivateTarget(
            RouteDefinition definition,
            NavigationContext<TData> context,
            int version)
        {
            var target = definition.DirectReference;
            if (target == null)
            {
                context.Cancel = true;
                context.Result = NavigationResult.Failed("DirectReference 为空");
                yield break;
            }

            target.SetActive(true);
            context.Result = NavigationResult.Succeeded(target);
            yield break;
        }

        private static IEnumerator PostProcessWrapper(
            IRouteTarget<TData> target,
            TData data,
            int version)
        {
            yield return null;

            // 如果新导航已发起，提前终止旧目标的后处理（避免重量级操作继续执行）
            if (RouteNavigator<TData>.CurrentVersion != version)
            {
                yield break;
            }

            try
            {
                target.StartPostProcess(data);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[Route] 后处理异常 [{target.GetType().Name}]: {e.Message}");
            }
        }
    }
}
