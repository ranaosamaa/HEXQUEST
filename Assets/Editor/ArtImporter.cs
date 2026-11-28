#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HexQuest.EditorTools
{
    /// <summary>
    /// Applies crisp import settings to everything in Resources/Art and Resources/Audio automatically:
    /// uncompressed, mipmapped, trilinear textures (sharp when zoomed in AND out) and streamed music.
    /// </summary>
    public class ArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() { return 2; }   // bump to force a reimport

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            bool big = assetPath.Contains("/title_");   // 2560x1440 backdrops: compress to save memory
            ti.textureCompression = big ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.isReadable = false;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = assetPath.Contains("/music_") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
        }

        [MenuItem("HexQuest/Reimport Art And Audio")]
        static void Reimport()
        {
            AssetDatabase.ImportAsset("Assets/Resources", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
