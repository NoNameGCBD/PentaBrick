using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    public enum Icon { Pause, Settings, Back, Code, Place, Paint, Erase, Undo, Redo, Camera, Restart, Link, Help, Home, Eye }

    /// <summary>
    /// 선 아이콘. WebGL에는 OS 글꼴이 없고 글꼴마다 기호가 다르므로 글자 대신 Painter2D로 그린다.
    /// 좌표는 0~1 상자 기준(y는 아래로 증가)이고 그릴 때 크기에 맞춰 늘린다.
    /// </summary>
    public static class Icons
    {
        public static VisualElement Make(Icon icon, float size, Color color)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.width = element.style.height = size;
            element.style.flexShrink = 0;
            element.generateVisualContent += context =>
            {
                float s = element.contentRect.width;
                if (s <= 0) return;
                var p = context.painter2D;
                p.strokeColor = color;
                p.fillColor = color;
                p.lineWidth = s * 0.09f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                Draw(p, icon, s);
            };
            return element;
        }

        private static void Draw(Painter2D p, Icon icon, float s)
        {
            Vector2 P(float x, float y) => new Vector2(x * s, y * s);
            void Line(params Vector2[] points)
            {
                p.BeginPath();
                p.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
                p.Stroke();
            }
            void Circle(float x, float y, float r, bool fill = false)
            {
                p.BeginPath();
                p.Arc(P(x, y), r * s, Angle.Degrees(0), Angle.Degrees(360));
                p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            // 원호를 점으로 펴서 돌려준다(좌우 반전이 쉽도록). 각도는 화면 기준(0°=오른쪽, 90°=아래).
            Vector2[] Arc(float cx, float cy, float r, float from, float to, bool mirror = false)
            {
                var points = new List<Vector2>();
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / 16f);
                    float x = cx + r * Mathf.Cos(a);
                    points.Add(P(mirror ? 1 - x : x, cy + r * Mathf.Sin(a)));
                }
                return points.ToArray();
            }

            switch (icon)
            {
                case Icon.Pause:
                    Line(P(0.38f, 0.26f), P(0.38f, 0.74f));
                    Line(P(0.62f, 0.26f), P(0.62f, 0.74f));
                    break;
                case Icon.Settings:
                    Circle(0.5f, 0.5f, 0.17f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Line(P(0.5f + 0.27f * Mathf.Cos(a), 0.5f + 0.27f * Mathf.Sin(a)), P(0.5f + 0.36f * Mathf.Cos(a), 0.5f + 0.36f * Mathf.Sin(a)));
                    }
                    break;
                case Icon.Back:
                    Line(P(0.76f, 0.5f), P(0.26f, 0.5f));
                    Line(P(0.46f, 0.3f), P(0.26f, 0.5f), P(0.46f, 0.7f));
                    break;
                case Icon.Code:
                    Line(P(0.44f, 0.22f), P(0.38f, 0.78f));
                    Line(P(0.64f, 0.22f), P(0.58f, 0.78f));
                    Line(P(0.24f, 0.4f), P(0.78f, 0.4f));
                    Line(P(0.22f, 0.6f), P(0.76f, 0.6f));
                    break;
                case Icon.Place:
                    Line(P(0.5f, 0.22f), P(0.5f, 0.78f));
                    Line(P(0.22f, 0.5f), P(0.78f, 0.5f));
                    break;
                case Icon.Paint: // 물방울
                {
                    var drop = new List<Vector2> { P(0.5f, 0.18f) };
                    drop.AddRange(Arc(0.5f, 0.6f, 0.21f, -35, 215));
                    drop.Add(P(0.5f, 0.18f));
                    Line(drop.ToArray());
                    break;
                }
                case Icon.Erase:
                    Line(P(0.28f, 0.28f), P(0.72f, 0.72f));
                    Line(P(0.72f, 0.28f), P(0.28f, 0.72f));
                    break;
                case Icon.Undo:
                case Icon.Redo:
                {
                    bool m = icon == Icon.Redo;
                    float X(float x) => m ? 1 - x : x;
                    var path = new List<Vector2> { P(X(0.28f), 0.4f) };
                    path.AddRange(Arc(0.58f, 0.57f, 0.17f, -90, 90, m));
                    path.Add(P(X(0.34f), 0.74f));
                    Line(path.ToArray());
                    Line(P(X(0.42f), 0.27f), P(X(0.28f), 0.4f), P(X(0.42f), 0.53f));
                    break;
                }
                case Icon.Camera:
                    Line(P(0.18f, 0.36f), P(0.36f, 0.36f), P(0.42f, 0.27f), P(0.58f, 0.27f), P(0.64f, 0.36f), P(0.82f, 0.36f), P(0.82f, 0.76f), P(0.18f, 0.76f), P(0.18f, 0.36f));
                    Circle(0.5f, 0.56f, 0.12f);
                    break;
                case Icon.Restart:
                {
                    const float end = 290f; // 오른쪽 위에 틈을 두고 시계 방향으로 돈다
                    var ring = Arc(0.5f, 0.52f, 0.25f, 0, end);
                    Line(ring);
                    float a = end * Mathf.Deg2Rad;
                    var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)); // 진행 방향
                    var tip = ring[ring.Length - 1];
                    Vector2 Wing(float deg) => tip - (Vector2)(Quaternion.Euler(0, 0, deg) * tangent) * (0.17f * s);
                    Line(Wing(40), tip, Wing(-40));
                    break;
                }
                case Icon.Link:
                    Circle(0.38f, 0.6f, 0.15f);
                    Circle(0.62f, 0.4f, 0.15f);
                    break;
                case Icon.Help:
                {
                    var hook = new List<Vector2>(Arc(0.5f, 0.37f, 0.15f, 180, 405));
                    hook.Add(P(0.5f, 0.62f));
                    Line(hook.ToArray());
                    Circle(0.5f, 0.77f, 0.045f, true);
                    break;
                }
                case Icon.Home:
                    Line(P(0.2f, 0.5f), P(0.5f, 0.24f), P(0.8f, 0.5f));
                    Line(P(0.3f, 0.44f), P(0.3f, 0.78f), P(0.7f, 0.78f), P(0.7f, 0.44f));
                    break;
                case Icon.Eye:
                    p.BeginPath();
                    p.MoveTo(P(0.16f, 0.5f));
                    p.QuadraticCurveTo(P(0.5f, 0.18f), P(0.84f, 0.5f));
                    p.QuadraticCurveTo(P(0.5f, 0.82f), P(0.16f, 0.5f));
                    p.Stroke();
                    Circle(0.5f, 0.5f, 0.09f, true);
                    break;
            }
        }
    }
}
