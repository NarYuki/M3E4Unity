#if UDONSHARP
using System.IO;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace M3E4Unity.Editor
{
    /// <summary>
    /// In VRChat projects, makes sure the M3E4Unity runtime assembly is known to UdonSharp
    /// (U# assembly definition) and that every behaviour has its UdonSharp program asset.
    /// The generated assets go to Assets/M3E4Unity.Generated/Udon because packages installed from
    /// git or a registry are read-only; existing program assets (anywhere) are reused.
    /// </summary>
    [InitializeOnLoad]
    public static class M3UdonSetup
    {
        const string RuntimeFolder = "Packages/com.m3e4unity/Runtime/Udon";
        const string AsmdefPath = RuntimeFolder + "/M3E4Unity.Runtime.asmdef";
        const string OutputFolder = M3ShapeAtlas.GeneratedFolder + "/Udon";

        static M3UdonSetup()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Tools/M3E4Unity/Developer/Regenerate UdonSharp Assets")]
        public static void Ensure()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool changed = false;
            var asmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(AsmdefPath);
            bool hasUsAsm = AssetDatabase.FindAssets("t:UdonSharpAssemblyDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Any(d => d != null && d.sourceAssembly == asmdef);
            if (!hasUsAsm && asmdef != null)
            {
                Directory.CreateDirectory(OutputFolder);
                var def = ScriptableObject.CreateInstance<UdonSharpAssemblyDefinition>();
                def.sourceAssembly = asmdef;
                AssetDatabase.CreateAsset(def, OutputFolder + "/M3E4Unity.Runtime.asset");
                changed = true;
            }
            var existing = AssetDatabase.FindAssets("t:UdonSharpProgramAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.sourceCsScript != null)
                .Select(p => p.sourceCsScript)
                .ToList();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { RuntimeFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var type = script != null ? script.GetClass() : null;
                if (type == null || !typeof(UdonSharpBehaviour).IsAssignableFrom(type)) continue;
                if (existing.Contains(script)) continue;
                Directory.CreateDirectory(OutputFolder);
                var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = script;
                AssetDatabase.CreateAsset(program, OutputFolder + "/" + Path.GetFileNameWithoutExtension(path) + ".asset");
                changed = true;
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                UdonSharpEditorUtility.ResetAssemblyCaches();
            }
        }
    }
}
#endif
