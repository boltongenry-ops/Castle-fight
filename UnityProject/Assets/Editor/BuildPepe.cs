using System;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildPepe {
    const string Project = "Assets/Pepe";
    const string Source = Project + "/Source";
    const string Resources = Project + "/Resources";
    const string Generated = Project + "/Generated";
    const string PackPath = Resources + "/PepeBrotherhood.asset";
    const string CatalogPath = Resources + "/pc-pepe.json";
    const string WindowsBundle = "pepe.windows";

    public static void Prepare() {
        Directory.CreateDirectory(Generated);
        AssetDatabase.Refresh();
        var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
        var data = JObject.Parse(catalog.text);
        var rows = (JArray)data["lines"];
        var art = JObject.Parse(File.ReadAllText(Source + "/presentation.json"));
        var textures = JObject.Parse(File.ReadAllText(Source + "/textures.json"));
        ImportTextures();
        var icons = HumanMatch.Strings(data["raceAbilityIds"]);
        var pack = ScriptableObject.CreateInstance<PcRacePack>();
        pack.catalog = catalog;
        pack.units = new GameObject[rows.Count * 2];
        pack.buildings = new GameObject[rows.Count * 2];
        pack.builders = new GameObject[2];
        pack.hdUnits = pack.hdBuildings = pack.hdBuilders = Array.Empty<GameObject>();
        pack.buildingIcons = rows.Select(row => Icon((string)row["building"])).ToArray();
        pack.builderIcon = Icon("pBld");
        pack.buildIcon = Icon("pB31");
        pack.abilityIds = icons;
        pack.abilityIcons = icons.Select(Icon).ToArray();
        for (int team = 0; team < 2; team++) {
            for (int row = 0; row < rows.Count; row++) {
                string building = (string)rows[row]["building"];
                string actor = (string)rows[row]["unit"];
                int index = team * rows.Count + row;
                pack.buildings[index] = Model(building, team, art[building], textures, true);
                if (!string.IsNullOrEmpty(actor)) pack.units[index] = Model(actor, team, art[actor], textures, false);
            }
            pack.builders[team] = Model("pBld", team, art["pBld"], textures, false);
        }
        SaveAsset(pack, PackPath);
        AssetDatabase.SaveAssets();
        ValidateArt();
    }

    static Texture2D Icon(string key) => AssetDatabase.LoadAssetAtPath<Texture2D>(Resources + "/Pepe/" + key + ".png");

    static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!existing) {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        UnityEngine.Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void ImportTextures() {
        var painting = (TextureImporter)AssetImporter.GetAtPath(Resources + "/Pepe/Versus-pepe.png");
        if (painting.npotScale != TextureImporterNPOTScale.None || painting.mipmapEnabled) {
            painting.npotScale = TextureImporterNPOTScale.None;
            painting.mipmapEnabled = false;
            painting.wrapMode = TextureWrapMode.Clamp;
            painting.SaveAndReimport();
        }
        foreach (string file in Directory.GetFiles(Project + "/Textures", "*.png")) {
            string path = file.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.GetPlatformTextureSettings("Android").overridden) continue;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            var mobile = importer.GetPlatformTextureSettings("Android");
            mobile.overridden = true;
            mobile.format = TextureImporterFormat.ETC2_RGBA8;
            mobile.maxTextureSize = 512;
            importer.SetPlatformTextureSettings(mobile);
            importer.SaveAndReimport();
        }
    }

    static GameObject Model(string raw, int team, JToken art, JObject textures, bool building) {
        string model = (string)art["model"];
        string sourcePath = Source + "/" + model + ".glb";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (!source) throw new InvalidDataException("Cannot import " + sourcePath);
        var root = new GameObject(raw + "-team" + team);
        try {
            var modelRoot = UnityEngine.Object.Instantiate(source, root.transform);
            modelRoot.name = "Swamp model";
            foreach (var animator in modelRoot.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
            foreach (var animation in modelRoot.GetComponentsInChildren<Animation>()) UnityEngine.Object.DestroyImmediate(animation);
            if (!building) modelRoot.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var view = root.AddComponent<PcModel>();
            view.raw = raw;
            view.appearanceKey = "pepe";
            view.animatedRoot = modelRoot;
            view.clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Select(original => {
                var clip = UnityEngine.Object.Instantiate(original);
                clip.legacy = true;
                var saved = SaveAsset(clip, Generated + "/" + model + "-" + original.name + ".anim");
                saved.name = original.name;
                EditorUtility.SetDirty(saved);
                return saved;
            }).ToArray();
            if (!building) {
                foreach (string state in new[] {"Stand", "Walk", "Attack", "Death"})
                    if (!view.clips.Any(clip => clip.name == state)) throw new InvalidDataException(raw + " has no " + state);
                view.clips.First(clip => clip.name == "Stand").SampleAnimation(modelRoot, .15f);
            }
            var renderers = modelRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidDataException(raw + " has no geometry");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = building ? Mathf.Min((float)art["size"] / Mathf.Max(bounds.size.x, bounds.size.z),
                (raw == "pB10" ? 5.25f : 4.25f) / bounds.size.y)
                : (float)art["height"] / bounds.size.y;
            if (!float.IsFinite(scale) || scale <= 0) throw new InvalidDataException(raw + " has no model bounds");
            view.visualScale = 1;
            modelRoot.transform.localScale *= scale;
            modelRoot.transform.localPosition = -scale * new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            int surface = 0;
            foreach (var renderer in renderers) {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    Material(raw, team, ++surface, original, textures[model], (bool?)art["elite"] == true)).ToArray();
            }
            string prefab = raw == "pBld" && team == 0 ? Resources + "/Pepe/Builder.prefab" : Generated + "/" + root.name + ".prefab";
            return PrefabUtility.SaveAsPrefabAsset(root, prefab);
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static Material Material(string raw, int team, int surface, Material original, JToken textures, bool elite) {
        var shader = Shader.Find("CastleFight/NativeUnit");
        if (!shader) throw new InvalidDataException("Missing native model shader");
        Color color = original.HasProperty("baseColorFactor") ? original.GetColor("baseColorFactor") :
            original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
        string name = original.name.ToLowerInvariant();
        if (name.Contains("quilt") || name.Contains("cloth") || name.Contains("leather") || name.Contains("cloth banner")) {
            var teamColor = team == 0 ? new Color(.32f,.64f,1f) : new Color(1f,.41f,.33f);
            color = new Color(teamColor.r,teamColor.g,teamColor.b,color.a);
        }
        if (elite && (name.Contains("bronze") || name.Contains("iron") || name.Contains("steel")))
            color = new Color(1f,.78f,.31f,color.a);
        bool transparent = name.Contains("membrane") || color.a < .98f;
        if (name.Contains("membrane")) color.a = .83f;
        string texture = (string)textures[original.name];
        if (texture == null && original.mainTexture)
            throw new InvalidDataException("Missing shared Pepe texture: " + raw + " / " + original.name);
        var material = new Material(shader) { mainTexture = texture == null ? SolidTexture() :
            AssetDatabase.LoadAssetAtPath<Texture2D>(Project + "/Textures/" + texture + ".png") };
        material.SetColor("_Tint", color);
        material.SetInt("_SrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
        material.SetInt("_DstBlend", (int)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        material.SetInt("_ZWrite", transparent ? 0 : 1);
        material.SetFloat("_Cutoff", .02f);
        material.renderQueue = transparent ? 3000 : 2450;
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "TransparentCutout");
        return SaveAsset(material, Generated + "/" + raw + "-t" + team + "-m" + surface + ".mat");
    }

    static Texture2D SolidTexture() {
        const string path = Generated + "/solid-color.asset";
        var solid = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (solid) return solid;
        solid = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        solid.SetPixel(0, 0, Color.white);
        solid.Apply();
        return SaveAsset(solid, path);
    }

    public static void ValidateArt() {
        var pack = AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath);
        if (!pack || !pack.catalog || pack.units.Count(p => p) != 20 || pack.buildings.Count(p => p) != 32
            || pack.builders.Count(p => p) != 2 || pack.buildingIcons.Any(icon => !icon) || pack.abilityIcons.Any(icon => !icon))
            throw new InvalidDataException("Incomplete Pepe race pack");
        if (pack.Prefabs.Any(p => !p.GetComponent<PcModel>() || p.GetComponentsInChildren<Renderer>().Any(
            r => r.sharedMaterials.Any(m => !m || !m.mainTexture)))) throw new InvalidDataException("Invalid Pepe visuals");
        Debug.Log("CF_PEPE_ART_PASS units=10 buildings=16 builders=1 teams=2");
    }

    public static void Windows() {
        Prepare();
        string directory = "Builds/PepeWindows";
        Directory.CreateDirectory(directory);
        var assets = new[] {PackPath, CatalogPath, AssetDatabase.GetAssetPath(
            AssetDatabase.LoadAssetAtPath<PcRacePack>(PackPath).builders[0])}
            .Concat(Directory.GetFiles(Resources + "/Pepe", "*.png").Select(path => path.Replace('\\', '/'))).ToArray();
        var addresses = new[] {"PepeBrotherhood", "pc-pepe", "Pepe/Builder"}
            .Concat(assets.Skip(3).Select(path => "Pepe/" + Path.GetFileNameWithoutExtension(path))).ToArray();
        var bundle = new AssetBundleBuild {assetBundleName=WindowsBundle, assetNames=assets, addressableNames=addresses};
        if (!BuildPipeline.BuildAssetBundles(directory, new[] {bundle}, BuildAssetBundleOptions.UncompressedAssetBundle |
            BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64))
            throw new IOException("Pepe Windows asset bundle did not build");
        string media = "Builds/MapWindows/CastleFightMap_Data/StreamingAssets/Frontend";
        Directory.CreateDirectory(media);
        File.Copy(directory + "/" + WindowsBundle, media + "/" + WindowsBundle, true);
        BuildFrontendRich.RebuildPlayerOnlyWindows();
    }
}
