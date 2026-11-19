using System.Collections.Generic;
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

        // --- 메인 화면 등장 효과: 다섯 블록이 차례로 튀어나오고, 이어서 게임 이름이 튀어나온다 ---
        private float mainStart = -10f; // 메인 화면에 들어온 시각. 창이 열려 화면을 다시 그려도 처음부터 다시 하지 않는다
        private readonly List<VisualElement> mainBlocks = new List<VisualElement>();
        private VisualElement mainTitle, mainTagline, mainSubtitle;
        private const float PopDelay = 0.15f, PopGap = 0.09f, PopTime = 0.45f, TitlePop = 0.55f; // 초
        private float TitleStart => PopDelay + (mainBlocks.Count - 1) * PopGap + PopTime * 0.6f;
        private float MainPopEnd => TitleStart + TitlePop + 0.5f;

        /// <summary>메인 화면 위쪽의 다섯 색 블록(가운데가 가장 높은 계단).</summary>
        private void BuildBrandBlocks(VisualElement page)
        {
            var row = Row(page);
            row.name = "brand-blocks";
            row.style.alignItems = Align.FlexEnd;
            row.style.marginBottom = 26;
            mainBlocks.Clear();
            int size = portrait ? 34 : 42;
            for (int i = 0; i < 5; i++)
            {
                Color color = ColorValues[i + 1];
                var block = new VisualElement { pickingMode = PickingMode.Ignore };
                block.style.width = block.style.height = size;
                block.style.marginLeft = block.style.marginRight = portrait ? 4 : 6;
                block.style.marginBottom = (2 - Mathf.Abs(i - 2)) * size * 0.25f;
                block.style.backgroundColor = color;
                Round(block, 8);
                // 윗면은 밝게, 아랫면은 어둡게 해서 입체감을 준다.
                block.style.borderTopWidth = 3;
                block.style.borderTopColor = Color.Lerp(color, Color.white, 0.45f);
                block.style.borderBottomWidth = 6;
                block.style.borderBottomColor = Color.Lerp(color, Color.black, 0.4f);
                row.Add(block);
                mainBlocks.Add(block);
            }
        }

        /// <summary>등장 효과의 현재 모습을 적용한다. 끝난 뒤에는 최종 모습 그대로다.</summary>
        private void UpdateMainPop()
        {
            if (mainTitle == null) return;
            float t = Time.unscaledTime - mainStart;
            for (int i = 0; i < mainBlocks.Count; i++)
            {
                float p = Mathf.Clamp01((t - PopDelay - i * PopGap) / PopTime);
                var block = mainBlocks[i];
                block.style.opacity = Mathf.Clamp01(p * 4);
                block.style.scale = new Scale(Vector2.one * Mathf.Max(0f, BackOut(p, 2.6f)));
                block.style.translate = new Translate(0, 22 * (1 - p));
            }
            float q = Mathf.Clamp01((t - TitleStart) / TitlePop);
            mainTitle.style.opacity = Mathf.Clamp01(q * 3);
            mainTitle.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(0.4f, 1f, BackOut(q, 2.2f)));
            float fade = Mathf.Clamp01((t - TitleStart - TitlePop * 0.5f) / 0.4f);
            mainTagline.style.opacity = mainSubtitle.style.opacity = fade;
        }

        /// <summary>목표를 살짝 지나쳤다가 돌아오는 움직임(0 → 1). overshoot가 클수록 더 튄다.</summary>
        private static float BackOut(float p, float overshoot)
        {
            float x = p - 1;
            return 1 + (overshoot + 1) * x * x * x + overshoot * x * x;
        }
    }
}
