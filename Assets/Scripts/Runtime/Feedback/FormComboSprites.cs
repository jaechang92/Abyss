using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// P04 폼 연계 시각 피드백(프로토타입)이 쓰는 도형 스프라이트. 처음 요청할 때 1회 만들고 캐시한다 —
    /// 매 프레임 텍스처 · 스프라이트를 만들지 않는다. 색은 SpriteRenderer.color 로 입힌다(모두 흰색).
    /// 모든 도형은 1유닛 크기(PPU = 텍스처 한 변), 피벗 가운데다.
    /// </summary>
    public static class FormComboSprites
    {
        private const int RING_TEXTURE_SIZE = 64;
        private const float RING_INNER_RATIO = 0.84f;   // 안쪽 84% 투명 — 얇은 고리
        private const int WEDGE_TEXTURE_SIZE = 32;

        private static Sprite whiteSprite;
        private static Sprite ringSprite;
        private static Sprite wedgeSprite;

        /// <summary>1유닛 흰 사각형(문구 배경 · 속도선).</summary>
        public static Sprite White
        {
            get
            {
                if (whiteSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    whiteSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), texture.width);
                }
                return whiteSprite;
            }
        }

        /// <summary>지름 1유닛 얇은 원형 고리(A 예약 · 성공).</summary>
        public static Sprite Ring
        {
            get
            {
                if (ringSprite == null) ringSprite = CreateRingSprite();
                return ringSprite;
            }
        }

        /// <summary>오른쪽(+x)을 가리키는 1유닛 쐐기(B 준비 방향). 왼쪽은 SpriteRenderer.flipX 로 뒤집는다.</summary>
        public static Sprite Wedge
        {
            get
            {
                if (wedgeSprite == null) wedgeSprite = CreateWedgeSprite();
                return wedgeSprite;
            }
        }

        private static Sprite CreateRingSprite()
        {
            int size = RING_TEXTURE_SIZE;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float inner = center * RING_INNER_RATIO;
            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    pixels[y * size + x] = dist <= outer && dist >= inner ? Color.white : clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>왼쪽 변이 밑변, 오른쪽 가운데가 꼭짓점인 삼각형. 밑변 가운데를 파내 화살촉(쐐기) 모양으로 만든다.</summary>
        private static Sprite CreateWedgeSprite()
        {
            int size = WEDGE_TEXTURE_SIZE;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float notch = size * 0.35f;   // 밑변에서 파낸 깊이
            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float dy = Mathf.Abs(y - half) / half;   // 0(가운데) ~ 1(위아래 끝)
                float tipX = (size - 1) * (1f - dy);     // 이 높이에서 삼각형이 끝나는 x
                float notchX = notch * (1f - dy);        // 이 높이에서 파낸 부분이 끝나는 x
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = x <= tipX && x >= notchX ? Color.white : clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
