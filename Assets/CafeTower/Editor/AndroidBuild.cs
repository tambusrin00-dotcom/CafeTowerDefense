using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CafeTowerDefense.EditorTools
{
    /// <summary>
    /// Google Play 업로드용 서명된 AAB 빌드.
    ///  - 메뉴: Cafe → Android AAB 빌드 (서명)
    ///  - 명령줄: Unity.exe -batchmode -quit -buildTarget Android -projectPath "프로젝트" -executeMethod CafeTowerDefense.EditorTools.AndroidBuild.BuildBatch
    /// 서명 정보는 프로젝트 밖의 %USERPROFILE%\keystore\CafeTowerDefense.properties 에서 읽음 (git에 올라가지 않음).
    /// Unity는 키스토어 비밀번호를 ProjectSettings에 저장하지 않으므로 빌드할 때마다 이 파일에서 다시 읽어 넣음.
    /// </summary>
    public static class AndroidBuild
    {
        const string PackageName = "com.tambusrin.cafetowerdefense";
        const string ProductName = "카페 타워 디펜스";
        const string CompanyName = "tambusrin";
        /// <summary>스토어에 올릴 때마다 Version 과 VersionCode 를 올려야 함 (VersionCode는 정수, 항상 증가)</summary>
        const string Version = "1.0.8";
        const int VersionCode = 8;

        const string IconPath = "Assets/CafeTower/Icons/AppIcon.png";
        const string IconBackgroundPath = "Assets/CafeTower/Icons/AppIconBackground.png";
        const string IconForegroundPath = "Assets/CafeTower/Icons/AppIconForeground.png";

        /// <summary>메뉴에서 아이콘만 바로 적용 (빌드 없이 Player Settings에서 확인용)</summary>
        [MenuItem("Cafe/앱 아이콘 적용")]
        public static void ApplyIcon()
        {
            try
            {
                SetLauncherIcons();
                AssetDatabase.SaveAssets();
                Debug.Log("[AndroidBuild] 앱 아이콘 적용: 기본 + Android 적응형");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("앱 아이콘", e.Message, "확인");
            }
        }

        /// <summary>
        /// 기본 아이콘과 Android 적응형 아이콘을 지정.
        /// 적응형을 비워 두면 Android 8 이상 기기의 홈 화면에 Unity 로고가 나옴.
        /// </summary>
        static void SetLauncherIcons()
        {
            var icon = LoadIcon(IconPath);
            var background = LoadIcon(IconBackgroundPath);
            var foreground = LoadIcon(IconForegroundPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var slot in adaptive) slot.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
        }

        static Texture2D LoadIcon(string path)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true; // 투명한 전경 가장자리에 색이 번지지 않게
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) throw new Exception($"앱 아이콘이 없어요: {path}");
            return tex;
        }

        static string PropsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "keystore", "CafeTowerDefense.properties");

        static string OutputPath => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "Builds", $"CafeTowerDefense-{Version}.aab");

        [MenuItem("Cafe/Android AAB 빌드 (서명)")]
        public static void BuildFromMenu() => Build(false);

        public static void BuildBatch() => Build(true);

        static void Build(bool batch)
        {
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new Exception("Android Build Support 모듈이 설치되어 있지 않아요. Unity Hub → 설치 → 6000.6.10f1 → 모듈 추가에서 " +
                                        "Android Build Support (OpenJDK, Android SDK & NDK Tools 포함)를 설치해 주세요.");

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUserBuildSettings.buildAppBundle = true; // APK 대신 AAB

                // 런타임 머티리얼 에셋이 없으면 휴대폰에서 3D 물체가 모두 분홍색으로 나옴
                if (!CafeMaterials.Ensure())
                    throw new Exception("URP Lit 머티리얼을 만들지 못했어요. Console 로그를 확인해 주세요.");

                var props = ReadProps(PropsPath);
                ApplyPlayerSettings(props);
                // Unity 6.6 Android 빌드는 디스크의 ProjectSettings.asset 값을 쓰는 경우가 있어
                // (1.0.1 빌드가 1.0.0 / 코드 1로 나온 원인) 빌드 전에 반드시 저장
                AssetDatabase.SaveAssets();

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                };
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new Exception($"빌드 실패: {summary.result} (오류 {summary.totalErrors}개). Console 로그를 확인해 주세요.");

                VerifyGradleProject();
                string mapping = CopyMappingFile();
                string msg = $"서명된 AAB 빌드 완료\n{OutputPath}\n{summary.totalSize / (1024f * 1024f):0.0} MB · {summary.totalTime.TotalMinutes:0.0}분" +
                             (mapping != null ? $"\n가독화 파일: {mapping}" : "\n(가독화 파일 mapping.txt를 찾지 못했어요)");
                Debug.Log("[AndroidBuild] " + msg.Replace("\n", " | "));
                if (batch) EditorApplication.Exit(0);
                else
                {
                    EditorUtility.RevealInFinder(OutputPath);
                    EditorUtility.DisplayDialog("Android AAB 빌드", msg, "확인");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AndroidBuild] " + e.Message);
                if (batch) EditorApplication.Exit(1);
                else EditorUtility.DisplayDialog("Android AAB 빌드 실패", e.Message, "확인");
            }
        }

        static void ApplyPlayerSettings(Dictionary<string, string> props)
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            SetLauncherIcons();

            // Google Play 요구사항: 64비트(ARM64) 포함 → IL2CPP 필수
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // 설치된 최신 API

            // Play Console 경고 1: R8로 Java 코드 축소·난독화 → 빌드 후 mapping.txt(가독화 파일)를 AAB 옆에 복사
            PlayerSettings.Android.minifyRelease = true;

            // Play Console 경고 2: 네이티브(IL2CPP) 디버그 기호를 AAB 안에 포함 → Play가 자동 인식, 별도 업로드 불필요
            // (혹시 수동 업로드가 필요할 때를 위해 zip 파일도 함께 만듦)
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.format =
                Unity.Android.Types.DebugSymbolFormat.IncludeInBundle | Unity.Android.Types.DebugSymbolFormat.Zip;

            // 가로 화면 고정 (좌우 회전만 허용)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 서명
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Require(props, "storeFile");
            PlayerSettings.Android.keystorePass = Require(props, "storePassword");
            PlayerSettings.Android.keyaliasName = Require(props, "keyAlias");
            PlayerSettings.Android.keyaliasPass = Require(props, "keyPassword");
            if (!File.Exists(PlayerSettings.Android.keystoreName))
                throw new Exception($"키스토어 파일이 없어요: {PlayerSettings.Android.keystoreName}");
        }

        /// <summary>
        /// 실제로 빌드에 쓰인 Gradle 설정(launcher/build.gradle)을 읽어 버전 이름·코드와 R8 여부를 확인.
        /// 하나라도 다르면 업로드하기 전에 실패로 알려줌.
        /// </summary>
        static void VerifyGradleProject()
        {
            var bee = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "Bee");
            var gradle = Directory.Exists(bee)
                ? Directory.GetFiles(bee, "build.gradle", SearchOption.AllDirectories)
                    .Where(p => p.Replace('\\', '/').EndsWith("/launcher/build.gradle"))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
            if (gradle == null)
            {
                Debug.LogWarning("[AndroidBuild] launcher/build.gradle을 찾지 못해 버전 확인을 건너뜀");
                return;
            }
            var text = File.ReadAllText(gradle);
            var code = System.Text.RegularExpressions.Regex.Match(text, @"versionCode\s+(\d+)").Groups[1].Value;
            var name = System.Text.RegularExpressions.Regex.Match(text, @"versionName\s+""([^""]+)""").Groups[1].Value;
            bool minify = System.Text.RegularExpressions.Regex.IsMatch(text, @"minifyEnabled\s+true");
            var problems = new List<string>();
            if (code != VersionCode.ToString()) problems.Add($"버전 코드 {code} (기대값 {VersionCode})");
            if (name != Version) problems.Add($"버전 이름 {name} (기대값 {Version})");
            if (!minify) problems.Add("R8(minify)이 꺼져 있음");
            var res = Path.Combine(Path.GetDirectoryName(gradle), "src", "main", "res", "mipmap-xxxhdpi");
            if (!IconLayerOk(Path.Combine(res, "ic_launcher_background.png"), true) ||
                !IconLayerOk(Path.Combine(res, "ic_launcher_foreground.png"), false))
                problems.Add("Android 적응형 아이콘이 카페 아이콘으로 들어가지 않음");
            if (problems.Count > 0)
                throw new Exception("빌드는 끝났지만 설정이 반영되지 않았어요. 이 AAB는 올리지 마세요.\n- " + string.Join("\n- ", problems));
            Debug.Log($"[AndroidBuild] 확인 완료: 버전 {name} · 코드 {code} · R8 켜짐 · 적응형 아이콘");
        }

        /// <summary>레이어 파일이 있고, 배경은 모서리가 불투명·전경은 투명한지 (Unity 기본 아이콘은 xxxhdpi가 없음)</summary>
        static bool IconLayerOk(string path, bool opaqueCorner)
        {
            if (!File.Exists(path)) return false;
            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return false;
                return tex.GetPixel(0, 0).a > 0.5f == opaqueCorner;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// R8이 만든 mapping.txt(Unity가 Library 안의 Gradle 프로젝트에 남김)를 찾아
        /// Builds/CafeTowerDefense-버전-mapping.txt 로 복사. Play Console의 'App Bundle 탐색기 → 가독화 파일'에 올리면 됨.
        /// </summary>
        static string CopyMappingFile()
        {
            var lib = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library");
            if (!Directory.Exists(lib)) return null;
            var newest = Directory.GetFiles(lib, "mapping.txt", SearchOption.AllDirectories)
                .Where(p => p.Replace('\\', '/').Contains("/mapping/"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (newest == null) return null;
            var dest = Path.Combine(Path.GetDirectoryName(OutputPath), $"CafeTowerDefense-{Version}-mapping.txt");
            File.Copy(newest, dest, true);
            return dest;
        }

        static string[] Scenes()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return list.Length > 0 ? list : new[] { "Assets/Scenes/SampleScene.unity" };
        }

        static Dictionary<string, string> ReadProps(string path)
        {
            if (!File.Exists(path)) throw new Exception($"서명 정보 파일이 없어요: {path}");
            var d = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int i = line.IndexOf('=');
                if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            return d;
        }

        static string Require(Dictionary<string, string> d, string key) =>
            d.TryGetValue(key, out var v) && v.Length > 0 ? v : throw new Exception($"서명 정보 파일에 '{key}' 항목이 없어요: {PropsPath}");
    }
}
