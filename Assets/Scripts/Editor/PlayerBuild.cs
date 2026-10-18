using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Jam > Build Windows Player: the game's name, company, icon (Assets/Art/UI/Icon.png, Tools/bake_icon.py) and no Unity
// splash, then a 64-bit Windows build of the three scenes into Builds/Windows (git-ignored), shown in Explorer. With
// Town Config > Footage Mode on it goes into Builds/Trailer instead, so the trailer build is never the one submitted.
// Rebuild the scenes first (Jam > Build Game Scenes) so the build has the latest.
public static class PlayerBuild
{
    const string ProductName = "Depths of the CrystalFall";
    const string CompanyName = "Semih Buğday";
    const string IconPath = "Assets/Art/UI/Icon.png";
    const string OutputFolder = "Builds/Windows", TrailerFolder = "Builds/Trailer";
    const string TownConfigPath = "Assets/Data/TownConfig.asset";
    static readonly string[] Scenes = { "Assets/Scenes/Title.unity", "Assets/Scenes/Town.unity", "Assets/Scenes/Arena.unity" };

    [MenuItem("Jam/Build Windows Player")]
    static void BuildWindows()
    {
        ApplyPlayerSettings();
        TownConfig config = AssetDatabase.LoadAssetAtPath<TownConfig>(TownConfigPath);
        bool trailer = config != null && config.footageMode;
        if (trailer) Debug.LogWarning("Town Config > Footage Mode is on: this is the trailer build (Builds/Trailer). Turn it off for the real one.");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = $"{(trailer ? TrailerFolder : OutputFolder)}/{ProductName}.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build done: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s)");
            EditorUtility.RevealInFinder(summary.outputPath);
        }
        else Debug.LogError($"Build {summary.result}: {summary.totalErrors} error(s), see the Console.");
    }

    static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"{IconPath} not found (python Tools/bake_icon.py): Unity's default icon.");
            return;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) }, IconKind.Any);
    }
}
