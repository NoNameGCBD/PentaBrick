using PentaBrick.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PentaBrick.View
{
    public sealed partial class GameUI
    {
        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }

        // 장식은 입력을 가로채지 않는다. 외부 이미지 없이 UI Toolkit으로 그린다.
        private static void AddPagePattern(VisualElement page)
        {
            var pattern = new VisualElement { pickingMode = PickingMode.Ignore };
            pattern.style.position = Position.Absolute;
            pattern.style.left = pattern.style.right = pattern.style.top = pattern.style.bottom = 0;
            pattern.style.overflow = Overflow.Hidden;
            page.Add(pattern);
            for (int i = 0; i < 22; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.position = Position.Absolute;
                line.style.left = Length.Percent(i * 5);
                line.style.top = line.style.bottom = 0;
                line.style.width = 1;
                line.style.backgroundColor = new Color(0.38f, 0.67f, 0.8f, 0.035f);
                pattern.Add(line);
            }
            for (int i = 0; i < 18; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.position = Position.Absolute;
                line.style.top = Length.Percent(i * 6);
                line.style.left = line.style.right = 0;
                line.style.height = 1;
                line.style.backgroundColor = new Color(0.38f, 0.67f, 0.8f, 0.035f);
                pattern.Add(line);
            }
        }

        private void BuildBrandPuzzle(VisualElement page)
        {
            var emblem = Row(page);
            emblem.name = "brand-puzzle";
            emblem.style.alignItems = Align.Center;
            emblem.style.marginBottom = 24;
            // 첫 샘플의 실제 평면도로 게임의 규칙을 시각화한다.
            var views = SamplePuzzles.All[0].Views;
            for (int i = 0; i < 3; i++)
            {
                var tile = new VisualElement { pickingMode = PickingMode.Ignore };
                tile.style.paddingLeft = tile.style.paddingRight = tile.style.paddingTop = tile.style.paddingBottom = 9;
                tile.style.marginLeft = tile.style.marginRight = 5;
                tile.style.backgroundColor = CardColor;
                Round(tile, 12);
                Border(tile, i == 1 ? SelectColor : LineColor, 1);
                if (i != 1) tile.style.translate = new Translate(0, 10);
                emblem.Add(tile);
                Grid(tile, null, views[i], i == 1 ? 24 : 19);
            }
        }

    }
}
