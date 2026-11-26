using UnityEditor;
using UnityEngine;

namespace CafeTowerDefense.EditorTools
{
    /// <summary>
    /// 배경 음악 가져오기 설정: Vorbis 압축 + 스트리밍 (원본 WAV 15MB → 앱 용량 약 1~2MB, 메모리 절약)
    /// </summary>
    public class CafeAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("CafeTower/Resources/Audio/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = false;
            imp.loadInBackground = true;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
        }
    }
}
