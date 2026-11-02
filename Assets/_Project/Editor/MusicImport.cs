using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Soundtrack import: streamed from disk (minutes of music would otherwise sit decompressed in memory) and
    /// Vorbis-compressed. Applies to everything under Resources/Music (made by Audio/split_soundtrack.py.txt).
    /// </summary>
    class MusicImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }
    }
}
