using UnityEditor;

namespace ParvaMirabilia.Editor
{
    public sealed class DuelArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            bool interfaceSprite = assetPath.Contains("/DiceYahzee/");
            importer.textureType = interfaceSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (interfaceSprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 512;
            if (assetPath.Contains("/Textures/"))
            {
                importer.maxTextureSize = 1024;
                importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            }
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
