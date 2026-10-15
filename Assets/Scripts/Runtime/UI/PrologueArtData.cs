using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>동일 600×280 원점의 납품 9PNG. 하나라도 없거나 규격이 다르면 도형을 사용한다.</summary>
    internal sealed class PrologueArtData
    {
        private const string RESOURCE_ROOT = "Art/Story/Prologue/";
        private static readonly string[] Names =
        {
            "01-background", "02-background", "03-background", "04-background",
            "01-falling-body", "02-hand", "03-lamp-strip", "04-body-trace", "04-shadow"
        };

        private readonly Texture2D[] textures;

        private PrologueArtData(Texture2D[] textures) => this.textures = textures;

        public Texture2D Background(int index) => textures[index];
        public Texture2D FallingBody => textures[4];
        public Texture2D Hand => textures[5];
        public Texture2D LampStrip => textures[6];
        public Texture2D BodyTrace => textures[7];
        public Texture2D Shadow => textures[8];

        public static PrologueArtData TryLoad()
        {
            var textures = new Texture2D[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                var texture = Resources.Load<Texture2D>(RESOURCE_ROOT + Names[i]);
                int width = i == 6 ? 1800 : 600;
                if (texture == null || texture.width != width || texture.height != 280 ||
                    texture.filterMode != FilterMode.Point || texture.mipmapCount != 1)
                    return null;
                textures[i] = texture;
            }
            return new PrologueArtData(textures);
        }
    }
}
