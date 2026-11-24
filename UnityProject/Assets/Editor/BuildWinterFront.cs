using System.IO;
using UnityEditor;

public static class BuildWinterFront {
    public static void Windows(){
        const string output="Builds/WinterFront";
        Directory.CreateDirectory(output);
        var bundle=new AssetBundleBuild{
            assetBundleName="winter-front.windows",
            assetNames=new[]{"Assets/WinterBattle/Resources/WinterBattle/WinterFront.shader"},
            addressableNames=new[]{"WinterFront"}
        };
        if(!BuildPipeline.BuildAssetBundles(output,new[]{bundle},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64))
            throw new System.Exception("Winter front bundle failed");
    }
}
