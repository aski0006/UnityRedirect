using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// 管道/拦截器链管理面板。
    /// 右侧面板：管理通用拦截器和类型专属拦截器的配置。
    /// </summary>
    public class PipelinePanel
    {
        private readonly VisualElement _root;

        private readonly ListView _globalInterceptorList;
        private readonly ListView _typedInterceptorList;
        private readonly Button _addGlobalButton;
        private readonly Button _addTypedButton;

        private RouteDefinition _currentRoute;
        private List<InterceptorConfig> _globalConfigs = new();
        private List<InterceptorConfig> _typedConfigs = new();
        private int _globalSelectionIndex = -1;
        private int _typedSelectionIndex = -1;

        public PipelinePanel(VisualElement root)
        {
            _root = root;

            _globalInterceptorList = root.Q<ListView>("global-interceptor-list");
            _typedInterceptorList = root.Q<ListView>("typed-interceptor-list");
            _addGlobalButton = root.Q<Button>("add-global-interceptor-button");
            _addTypedButton = root.Q<Button>("add-typed-interceptor-button");

            ConfigureInterceptorLists();
        }

        private void ConfigureInterceptorLists()
        {
            // ── Global Interceptor List ──
            _globalInterceptorList.makeItem = () => CreateInterceptorItem();
            _globalInterceptorList.bindItem = (element, index) =>
                BindInterceptorItem(element, index, _globalConfigs);
            _globalInterceptorList.itemsSource = _globalConfigs;
            _globalInterceptorList.onSelectionChange += sel =>
            {
                _globalSelectionIndex = _globalInterceptorList.selectedIndex;
            };

            _addGlobalButton.clicked += () =>
            {
                ShowAddInterceptorDialog(true);
            };

            // ── Typed Interceptor List ──
            _typedInterceptorList.makeItem = () => CreateInterceptorItem();
            _typedInterceptorList.bindItem = (element, index) =>
                BindInterceptorItem(element, index, _typedConfigs);
            _typedInterceptorList.itemsSource = _typedConfigs;
            _typedInterceptorList.onSelectionChange += sel =>
            {
                _typedSelectionIndex = _typedInterceptorList.selectedIndex;
            };

            _addTypedButton.clicked += () =>
            {
                ShowAddInterceptorDialog(false);
            };
        }

        private VisualElement CreateInterceptorItem()
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 3,
                    paddingBottom = 3,
                    alignItems = Align.Center,
                }
            };

            var toggle = new Toggle
            {
                name = "interceptor-toggle",
                style =
                {
                    marginRight = 4,
                    flexGrow = 0,
                }
            };

            var label = new Label
            {
                name = "interceptor-label",
                style =
                {
                    fontSize = 11,
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            var deleteButton = new Button
            {
                name = "interceptor-delete",
                text = "×",
                style =
                {
                    fontSize = 12,
                    width = 18,
                    height = 18,
                    paddingLeft = 0,
                    paddingRight = 0,
                    backgroundColor = new StyleColor(new Color(0.5f, 0.2f, 0.2f)),
                    color = Color.white,
                }
            };

            container.Add(toggle);
            container.Add(label);
            container.Add(deleteButton);

            return container;
        }

        private void BindInterceptorItem(VisualElement element, int index, List<InterceptorConfig> configs)
        {
            if (index < 0 || index >= configs.Count) return;

            var config = configs[index];
            var toggle = element.Q<Toggle>("interceptor-toggle");
            var label = element.Q<Label>("interceptor-label");
            var deleteButton = element.Q<Button>("interceptor-delete");

            if (toggle != null)
            {
                toggle.value = config.Enabled;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    // We'd ideally use SerializedProperty, but for simplicity
                    // we track changes directly.
                    SaveInterceptorConfigs();
                });
            }

            if (label != null)
            {
                var typeName = config.TypeName;
                // Short display name
                var shortName = typeName;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var lastDot = typeName.LastIndexOf('.');
                    if (lastDot >= 0) shortName = typeName.Substring(lastDot + 1);
                }

                label.text = shortName ?? "(unknown)";
                label.tooltip = typeName;
            }

            if (deleteButton != null)
            {
                // Unregister old click events
                deleteButton.clickable = null;
                deleteButton.clicked += () =>
                {
                    configs.RemoveAt(index);
                    SaveInterceptorConfigs();
                    Refresh();
                };
            }
        }

        private void ShowAddInterceptorDialog(bool isGlobal)
        {
            var menu = new GenericMenu();

            // Scan for available interceptor types
            var interceptorTypes = FindInterceptorTypes(isGlobal);

            if (interceptorTypes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No interceptors found"));
            }
            else
            {
                foreach (var type in interceptorTypes)
                {
                    var typeName = type.FullName;
                    var shortName = type.Name;
                    menu.AddItem(new GUIContent(shortName), false, () =>
                    {
                        AddInterceptor(typeName, isGlobal);
                    });
                }
            }

            menu.ShowAsContext();
        }

        private List<Type> FindInterceptorTypes(bool isGlobal)
        {
            var results = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

                try
                {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.IsAbstract || type.IsInterface) continue;
                        if (!type.IsClass) continue;

                        if (isGlobal)
                        {
                            // Implements INavigationInterceptorBase
                            if (typeof(INavigationInterceptorBase).IsAssignableFrom(type))
                            {
                                results.Add(type);
                            }
                        }
                        else
                        {
                            // Implements INavigationInterceptor<TData> (any TData)
                            if (type.GetInterfaces().Any(i =>
                                i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(INavigationInterceptor<>)))
                            {
                                results.Add(type);
                            }
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) { }
            }

            return results.OrderBy(t => t.Name).ToList();
        }

        private void AddInterceptor(string typeName, bool isGlobal)
        {
            // Resolve the type — typeName comes from FindInterceptorTypes as FullName
            // We need AssemblyQualifiedName for proper serialization
            Type resolvedType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                resolvedType = asm.GetType(typeName);
                if (resolvedType != null) break;
            }
            if (resolvedType == null)
            {
                Debug.LogWarning($"[RouteNavigator] Cannot resolve interceptor type: {typeName}");
                return;
            }

            var qualifiedName = resolvedType.AssemblyQualifiedName;

            if (isGlobal)
            {
                AddGlobalInterceptor(qualifiedName);
            }
            else
            {
                AddTypedInterceptor(resolvedType, qualifiedName);
            }

            Refresh();
        }

        private void AddGlobalInterceptor(string qualifiedName)
        {
            var core = RouteCore.Instance;
            if (core == null)
            {
                Debug.LogWarning("[RouteNavigator] RouteCore not found. Please init assets first.");
                return;
            }

            var so = new SerializedObject(core);
            var configsProp = so.FindProperty("globalInterceptorConfigs");
            if (configsProp == null)
            {
                Debug.LogWarning("[RouteNavigator] Cannot find globalInterceptorConfigs on RouteCore. Check field name.");
                return;
            }

            configsProp.InsertArrayElementAtIndex(configsProp.arraySize);
            var newConfig = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
            var typeNameProp = newConfig.FindPropertyRelative("typeName");
            if (typeNameProp != null) typeNameProp.stringValue = qualifiedName;
            var enabledProp = newConfig.FindPropertyRelative("enabled");
            if (enabledProp != null) enabledProp.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(core);

            // Reload configs from serialized data
            ReloadGlobalConfigs();
        }

        private void AddTypedInterceptor(Type interceptorType, string qualifiedName)
        {
            // Find the TData from INavigationInterceptor<TData>
            Type dataType = null;
            foreach (var iface in interceptorType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(INavigationInterceptor<>))
                {
                    dataType = iface.GetGenericArguments()[0];
                    break;
                }
            }

            if (dataType == null)
            {
                Debug.LogWarning("[RouteNavigator] Cannot determine TData for typed interceptor. " +
                        "The type must implement INavigationInterceptor<TData>.");
                return;
            }

            // Find or create RouteRegistry
            var registry = Resources.Load<RouteRegistry>("RouteNavigatorDatabase/RouteRegistry");
            if (registry == null)
            {
                Debug.LogWarning("[RouteNavigator] RouteRegistry not found. Please init assets first.");
                return;
            }

            var so = new SerializedObject(registry);
            var groupsProp = so.FindProperty("typedGroups");
            if (groupsProp == null) return;

            // Find or create group for this dataType
            var groupIndex = -1;
            var dataTypeFullName = dataType.FullName;
            for (var i = 0; i < groupsProp.arraySize; i++)
            {
                var elem = groupsProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("dataTypeFullName")?.stringValue == dataTypeFullName)
                {
                    groupIndex = i;
                    break;
                }
            }

            if (groupIndex < 0)
            {
                // Create new group
                groupsProp.InsertArrayElementAtIndex(groupsProp.arraySize);
                groupIndex = groupsProp.arraySize - 1;
                var newGroup = groupsProp.GetArrayElementAtIndex(groupIndex);
                var nameProp = newGroup.FindPropertyRelative("dataTypeFullName");
                if (nameProp != null) nameProp.stringValue = dataTypeFullName;
            }

            // Add interceptor config to the group
            var group = groupsProp.GetArrayElementAtIndex(groupIndex);
            var interceptorListProp = group.FindPropertyRelative("interceptors");
            if (interceptorListProp == null) return;

            interceptorListProp.InsertArrayElementAtIndex(interceptorListProp.arraySize);
            var config = interceptorListProp.GetArrayElementAtIndex(interceptorListProp.arraySize - 1);
            var typeProp = config.FindPropertyRelative("typeName");
            if (typeProp != null) typeProp.stringValue = qualifiedName;
            var enabledProp = config.FindPropertyRelative("enabled");
            if (enabledProp != null) enabledProp.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);

            // Reload configs from serialized data
            ReloadTypedConfigs();
        }

        private void SaveInterceptorConfigs()
        {
            var core = RouteCore.Instance;
            if (core != null)
            {
                EditorUtility.SetDirty(core);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>刷新显示指定路由的拦截器配置</summary>
        public void Display(RouteDefinition route)
        {
            _currentRoute = route;

            ReloadGlobalConfigs();
            ReloadTypedConfigs();

            Refresh();
        }

        private void ReloadGlobalConfigs()
        {
            var core = RouteCore.Instance;
            _globalConfigs.Clear();
            if (core == null) return;

            var so = new SerializedObject(core);
            var configsProp = so.FindProperty("globalInterceptorConfigs");
            if (configsProp == null) return;

            for (var i = 0; i < configsProp.arraySize; i++)
            {
                var configElement = configsProp.GetArrayElementAtIndex(i);
                var typeName = configElement.FindPropertyRelative("typeName")?.stringValue ?? string.Empty;
                var enabled = configElement.FindPropertyRelative("enabled")?.boolValue ?? true;
                var orderOverride = configElement.FindPropertyRelative("orderOverride")?.intValue ?? 0;

                var config = new InterceptorConfig();
                SetInterceptorConfigFields(config, typeName, enabled, orderOverride);
                _globalConfigs.Add(config);
            }
        }

        private void ReloadTypedConfigs()
        {
            var registry = Resources.Load<RouteRegistry>("RouteNavigatorDatabase/RouteRegistry");
            _typedConfigs.Clear();
            if (registry == null) return;

            var so = new SerializedObject(registry);
            var groupsProp = so.FindProperty("typedGroups");
            if (groupsProp == null) return;

            // Flatten all interceptor configs from all groups for display
            for (var g = 0; g < groupsProp.arraySize; g++)
            {
                var group = groupsProp.GetArrayElementAtIndex(g);
                var dataTypeName = group.FindPropertyRelative("dataTypeFullName")?.stringValue;
                var interceptorListProp = group.FindPropertyRelative("interceptors");
                if (interceptorListProp == null) continue;

                for (var i = 0; i < interceptorListProp.arraySize; i++)
                {
                    var configElement = interceptorListProp.GetArrayElementAtIndex(i);
                    var typeName = configElement.FindPropertyRelative("typeName")?.stringValue ?? string.Empty;
                    var enabled = configElement.FindPropertyRelative("enabled")?.boolValue ?? true;
                    var orderOverride = configElement.FindPropertyRelative("orderOverride")?.intValue ?? 0;

                    var config = new InterceptorConfig();
                    SetInterceptorConfigFields(config, typeName, enabled, orderOverride);
                    _typedConfigs.Add(config);
                }
            }
        }

        private static void SetInterceptorConfigFields(InterceptorConfig config, string typeName, bool enabled, int orderOverride)
        {
            var type = typeof(InterceptorConfig);
            type.GetField("typeName", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(config, typeName);
            type.GetField("enabled", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(config, enabled);
            type.GetField("orderOverride", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(config, orderOverride);
        }

        private void Refresh()
        {
            _globalInterceptorList.itemsSource = _globalConfigs;
            _typedInterceptorList.itemsSource = _typedConfigs;
            _globalInterceptorList.Rebuild();
            _typedInterceptorList.Rebuild();
        }
    }
}
