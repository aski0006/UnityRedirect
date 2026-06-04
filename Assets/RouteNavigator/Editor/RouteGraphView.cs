using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// 路由图可视化视图。
    /// 展示路由定义之间的依赖/跳转关系图。
    /// 使用 UI Toolkit 的 GraphView 或简单节点布局。
    /// </summary>
    public class RouteGraphView : VisualElement
    {
        private readonly VisualElement _container;

        public RouteGraphView()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            var toolbar = new Toolbar();
            var refreshButton = new ToolbarButton(() => Refresh())
            {
                text = "Refresh Graph"
            };
            toolbar.Add(refreshButton);
            Add(toolbar);

            // Scrollable container for graph nodes
            _container = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f)),
                }
            };

            var placeholder = new Label("Route Graph\n\nSelect a route type to visualize its navigation graph.")
            {
                style =
                {
                    fontSize = 14,
                    color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    flexGrow = 1,
                    paddingTop = 40,
                }
            };
            _container.Add(placeholder);

            Add(_container);
        }

        /// <summary>刷新图形显示</summary>
        public void Refresh()
        {
            _container.Clear();

            // Build graph from RouteCore
            var core = RouteCore.Instance;
            if (core == null)
            {
                _container.Add(new Label("RouteCore not found. Create a RouteCore asset in Resources.")
                {
                    style =
                    {
                        fontSize = 12,
                        color = new StyleColor(new Color(1.0f, 0.5f, 0.5f)),
                        paddingLeft = 12,
                        paddingRight = 12,
                        paddingTop = 12,
                        paddingBottom = 12,
                    }
                });
                return;
            }

            var routes = core.Routes;
            if (routes == null || routes.Count == 0)
            {
                _container.Add(new Label("No routes defined.")
                {
                    style =
                    {
                        fontSize = 12,
                        color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)),
                        paddingLeft = 12,
                        paddingRight = 12,
                        paddingTop = 12,
                        paddingBottom = 12,
                    }
                });
                return;
            }

            // Simple vertical node list for now
            foreach (var route in routes)
            {
                if (route == null) continue;

                var node = CreateRouteNode(route);
                _container.Add(node);
            }
        }

        private VisualElement CreateRouteNode(RouteDefinition route)
        {
            var node = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.32f)),
                    marginLeft = 12,
                    marginRight = 12,
                    marginTop = 4,
                    marginBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderLeftColor = GetTargetTypeColor(route.TargetType),
                    borderLeftWidth = 3,
                }
            };

            // Route ID
            var idLabel = new Label(route.RouteId ?? "?")
            {
                style =
                {
                    fontSize = 12,
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold),
                    color = Color.white,
                    minWidth = 120,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };
            node.Add(idLabel);

            // Display Name
            if (!string.IsNullOrEmpty(route.DisplayName))
            {
                var nameLabel = new Label(route.DisplayName)
                {
                    style =
                    {
                        fontSize = 11,
                        color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
                        unityTextAlign = TextAnchor.MiddleLeft,
                        flexGrow = 1,
                    }
                };
                node.Add(nameLabel);
            }

            // Target Type badge
            var badge = new Label(route.TargetType.ToString())
            {
                style =
                {
                    fontSize = 10,
                    color = GetTargetTypeColor(route.TargetType),
                    backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.18f)),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 4,
                    unityTextAlign = TextAnchor.MiddleCenter,
                }
            };
            node.Add(badge);

            // Load Mode badge
            var loadBadge = new Label(route.LoadMode.ToString())
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(new Color(0.6f, 0.8f, 0.6f)),
                    backgroundColor = new StyleColor(new Color(0.15f, 0.18f, 0.15f)),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 4,
                    unityTextAlign = TextAnchor.MiddleCenter,
                }
            };
            node.Add(loadBadge);

            return node;
        }

        private static Color GetTargetTypeColor(RouteTargetType type)
        {
            return type switch
            {
                RouteTargetType.Scene => new Color(0.4f, 0.6f, 1.0f),
                RouteTargetType.GameObjectInScene => new Color(0.4f, 1.0f, 0.4f),
                RouteTargetType.Prefab => new Color(1.0f, 0.7f, 0.3f),
                _ => Color.gray,
            };
        }
    }
}
