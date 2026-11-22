using System.IO;
using UnityEditor;
using UnityEngine;

namespace CafeTowerDefense.EditorTools
{
    /// <summary>
    /// 게임이 런타임에 쓰는 URP Lit 머티리얼을 Resources 폴더에 에셋으로 만들어 둠.
    /// 에셋으로 있어야 URP Lit 셰이더(와 발광 _EMISSION 변형)가 빌드에 포함됨.
    /// 없으면 휴대폰 빌드에서 셰이더가 빠져 모든 3D 물체가 분홍색으로 나옴.
    /// 에디터가 스크립트를 불러올 때, 그리고 Android 빌드 직전에 자동으로 확인함.
    /// </summary>
    public static class CafeMaterials
    {
        public const string Folder = "Assets/CafeTower/Resources";
        const string LitShaderName = "Universal Render Pipeline/Lit";

        [InitializeOnLoadMethod]
        static void AutoEnsure() => EditorApplication.delayCall += () => Ensure();

        [MenuItem("Cafe/머티리얼 다시 만들기")]
        static void Recreate() => Ensure(true);

        /// <returns>머티리얼이 모두 준비되었으면 true</returns>
        public static bool Ensure(bool force = false)
        {
            var shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[CafeMaterials] '{LitShaderName}' 셰이더를 찾지 못했어요. URP 패키지를 확인해 주세요.");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            bool changed = false;
            changed |= Make("CafeLit", shader, force, m =>
            {
                m.SetFloat("_Smoothness", 0.25f);
            });
            changed |= Make("CafeGloss", shader, force, m =>
            {
                m.SetFloat("_Smoothness", 0.9f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[CafeMaterials] 런타임 머티리얼을 만들었어요: " + Folder);
            }
            return true;
        }

        static bool Make(string name, Shader shader, bool force, System.Action<Material> setup)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !force && existing.shader == shader) return false;
            var m = new Material(shader) { name = name };
            setup(m);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return true;
        }
    }
}
