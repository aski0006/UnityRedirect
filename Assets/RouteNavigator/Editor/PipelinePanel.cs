using System;
using System.Collections.Generic;
using System.Linq;
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
            var config = new InterceptorConfig();
            // We'd need reflection to set the private fields.
            // For a production tool, we'd use SerializedProperty on RouteCore/Registry.
            // For now, store via RouteCore's serialized list.

            var list = isGlobal ? _globalConfigs : _typedConfigs;

            // Use serialization workaround: we know the field name in InterceptorConfig
            var configSo = new SerializedObject(_currentRoute); // placeholder
            // In practice, InterceptorConfig would be managed via RouteCore asset.

            // Since InterceptorConfig fields are private with [SerializeField],
            // we can create via SerializedProperty on RouteCore or registry asset.
            // For now, use a direct approach compatible with existing serialization.

            var type = System.Type.GetType(typeName);
            if (type == null) return;

            // Create config via ScriptableObject serialization context
            var core = RouteCore.Instance;
            if (core != null && isGlobal)
            {
                var so = new SerializedObject(core);
                var configsProp = so.FindProperty("globalInterceptorConfigs");
                if (configsProp != null)
                {
                    configsProp.InsertArrayElementAtIndex(configsProp.arraySize);
                    var newConfig = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
                    var typeNameProp = newConfig.FindPropertyRelative("typeName");
                    if (typeNameProp != null)
                    {
                        typeNameProp.stringValue = typeName;
                    }
                    var enabledProp = newConfig.FindPropertyRelative("enabled");
                    if (enabledProp != null)
                    {
                        enabledProp.boolValue = true;
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(core);
                }
            }

            Refresh();
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

            // Load global configs from RouteCore
            var core = RouteCore.Instance;
            if (core != null)
            {
                var so = new SerializedObject(core);
                var configsProp = so.FindProperty("globalInterceptorConfigs");
                _globalConfigs.Clear();
                if (configsProp != null)
                {
                    for (var i = 0; i < configsProp.arraySize; i++)
                    {
                        var configElement = configsProp.GetArrayElementAtIndex(i);
                        var typeName = configElement.FindPropertyRelative("typeName")?.stringValue;
                        var enabled = configElement.FindPropertyRelative("enabled")?.boolValue ?? true;

                        // Reconstruct InterceptorConfig for display
                        // This is a simplified approach; real implementation would use EditorGUILayout
                    }
                }
            }

            Refresh();
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
