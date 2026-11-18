using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CastleFight;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FitOptionalModels039 {
    const string FitTransform = "Classic size";
    const string ModelRoot = "Assets/GeneratedReforged/";
    const string IconRoot = "Assets/SourceAssets/Reforged/Portraits/";
    const string Vortex = "Assets/ArchivedReforged/RefFx/tornado";
    const string Report = "../Validation/Reforged039/size-and-icon-audit.json";
    static readonly string[] Teams = { "0", "1" };

    public static void PrepareAvailable() {
        var archive = JObject.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/manifest.json"));
        var ids = archive.Properties().Select(p => p.Name).Where(id => id.Length == 4).OrderBy(id => id).ToArray();
        string[] missingPrefabs = ids.Where(id => Teams.Any(team => !File.Exists(ModelRoot + id + "-t" + team + ".prefab"))).ToArray();
        if (missingPrefabs.Length != 0) BuildReforged.PrepareModels(missingPrefabs);
        Prepare(ids, archive);
    }

    public static void RetryAvailable() {
        var previous=JObject.Parse(File.ReadAllText(Report));
        var archive=JObject.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/manifest.json"));
        var ids=archive.Properties().Select(p=>p.Name).Where(raw=>raw.Length==4).ToHashSet(StringComparer.Ordinal);
        var passed=(JArray)previous["measurements"];
        var portraits=(JArray)previous["portraits"];
        var finished=passed.GroupBy(row=>(string)row["raw"])
            .Where(group=>group.Select(row=>(string)row["team"]).Distinct().Count()==2)
            .Select(group=>group.Key).ToHashSet(StringComparer.Ordinal);
        var failed=((JArray)previous["errors"]).Select(row=>(string)row["raw"]).ToHashSet(StringComparer.Ordinal);
        var requested=Environment.GetEnvironmentVariable("CF_REFORGED_REFRESH_MODELS");
        if(!string.IsNullOrWhiteSpace(requested)){
            var refresh=requested.Split(',').Where(ids.Contains).ToArray();
            BuildReforged.PrepareModels(refresh);
            failed.UnionWith(refresh);
        }
        var retry=ids.Where(raw=>!finished.Contains(raw)||failed.Contains(raw)).OrderBy(raw=>raw,StringComparer.Ordinal).ToArray();
        var attempted=retry.ToHashSet(StringComparer.Ordinal);
        Prepare(retry,archive,
            new JArray(passed.Where(row=>!attempted.Contains((string)row["raw"]))),
            new JArray(portraits.Where(raw=>(string)raw!="n02E"&&!attempted.Contains((string)raw))));
    }

    internal static void Prepare(string[] ids, JObject archive, JArray previousSamples=null, JArray previousPortraits=null) {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var upgradedActors = new HashSet<string>(StringComparer.Ordinal);
        var catalogs = Directory.GetFiles("Assets/Resources", "pc-*.json")
            .Where(path => Path.GetFileName(path) != "pc-ultimate.json");
        foreach (string path in catalogs) {
            if (!(JObject.Parse(File.ReadAllText(path))["lines"] is JArray lines)) continue;
            foreach (var line in lines) {
                string raw = (string)line["unit"];
                if (!string.IsNullOrEmpty(raw) && (bool?)line["base"] == false && (string)line["category"] != "summon")
                    upgradedActors.Add(raw);
            }
        }
        var profiles = JObject.Parse(File.ReadAllText("Assets/Resources/native-appearance.json"));
        var centaur = JObject.Parse(File.ReadAllText("Assets/Resources/pc-centaur.json"))["centaurAppearance"] as JObject;
        if (centaur != null) profiles.Merge(centaur);
        var hdProfiles = new JObject();
        foreach (var entry in archive.Properties()) hdProfiles[entry.Name + "_ref"] = entry.Value["appearance"].DeepClone();

        Directory.CreateDirectory(IconRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        var samples = previousSamples??new JArray();
        var errors = new JArray();
        var portraits = previousPortraits??new JArray();
        var actorIds = JArray.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/required-unit-models.json"))
            .Values<string>().ToHashSet(StringComparer.Ordinal);
        bool graphics = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        foreach (string raw in ids) {
            try {
                Bounds reference = raw == "hcas"
                    ? MeasureCastle(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedMap/Prefabs/castle0.prefab"))
                    : MeasureClassic(raw, profiles);
                foreach (string team in Teams) {
                    string path = ModelRoot + raw + "-t" + team + ".prefab";
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try {
                        var fit = root.transform.Find(FitTransform);
                        if (!fit) {
                            var children = root.transform.Cast<Transform>().ToArray();
                            fit = new GameObject(FitTransform).transform;
                            fit.SetParent(root.transform, false);
                            foreach (var child in children) child.SetParent(fit, false);
                        }
                        fit.localScale = Vector3.one;
                        fit.localPosition = Vector3.zero;
                        var original = MeasurePrefab(root, hdProfiles);
                        float scale = reference.size.y / original.size.y;
                        fit.localScale = Vector3.one * scale;
                        LimitFootprint(fit, reference.size, original.size * scale);
                        fit.localPosition = new Vector3(0, reference.min.y - original.min.y * scale, 0);
                        var scaled = MeasurePrefab(root, hdProfiles);
                        if (Mathf.Abs(reference.min.y - scaled.min.y) > .015f || Mathf.Abs(reference.size.y - scaled.size.y) > .015f)
                            throw new InvalidDataException($"Classic height/base {reference.size.y:F3}/{reference.min.y:F3}; measured {scaled.size.y:F3}/{scaled.min.y:F3} after scale {scale:F3}");
                        var hd = root.GetComponent<PcModel>();
                        hd.godotHeight = reference.size.y;
                        hd.visualScale = scale;
                        if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new IOException("Could not save " + path);
                        samples.Add(new JObject {
                            ["raw"] = raw, ["team"] = team, ["classic"] = Dimensions(reference),
                            ["before"] = Dimensions(original), ["after"] = Dimensions(scaled), ["scale"] = scale
                        });
                    } finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                if (actorIds.Contains(raw) && graphics) {
                    string path = ModelRoot + raw + "-t0.prefab";
                    var image = BuildCentaurIcons.Capture(AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        (JObject)hdProfiles[raw + "_ref"], 0, upgradedActors.Contains(raw), true);
                    try { File.WriteAllBytes(IconRoot + raw + ".png", image.EncodeToPNG()); }
                    finally { Object.DestroyImmediate(image); }
                    portraits.Add(raw);
                }
            } catch (Exception error) {
                errors.Add(new JObject { ["raw"] = raw, ["message"] = error.Message, ["details"] = error.ToString() });
                Debug.LogWarning("Reforged model fit " + raw + ": " + error.Message);
            }
        }
        if (graphics) {
            CaptureVortexIcon();
            portraits.Add("n02E");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText(Report, new JObject {
            ["status"] = errors.Count == 0 ? "PASS" : "INCOMPLETE", ["measurements"] = samples,
            ["portraits"] = portraits, ["errors"] = errors
        }.ToString());
        Debug.Log("CF_REFORGED_SIZE_AUDIT pairs=" + samples.Count + " portraits=" + portraits.Count + " errors=" + errors.Count);
        if (errors.Count != 0) throw new InvalidDataException("Reforged asset preparation failed; see " + Report);

    }

    public static void FitWideModels() {
        var audit=JObject.Parse(File.ReadAllText(Report));
        if((string)audit["status"]!="PASS")throw new InvalidDataException("Complete the HD measurements first");
        var archive=JObject.Parse(File.ReadAllText("Assets/SourceAssets/Reforged/manifest.json"));
        var profiles=new JObject();
        foreach(var entry in archive.Properties())profiles[entry.Name+"_ref"]=entry.Value["appearance"].DeepClone();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        int changed=0;
        foreach(var row in (JArray)audit["measurements"]){
            var target=(JArray)row["classic"];var current=(JArray)row["after"];
            Vector3 expected=Size(target),before=Size(current);
            if(before.x<=expected.x*1.15f&&before.z<=expected.z*1.15f)continue;
            string path=ModelRoot+(string)row["raw"]+"-t"+(string)row["team"]+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var fit=root.transform.Find(FitTransform);
                if(!fit)throw new InvalidDataException("Missing fitted model "+path);
                LimitFootprint(fit,expected,before);
                var actual=MeasurePrefab(root,profiles);
                if(actual.size.x>expected.x*1.15f+.01f||actual.size.z>expected.z*1.15f+.01f||
                    Mathf.Abs(actual.size.y-expected.y)>.015f||Mathf.Abs(actual.min.y-(float)target[3])>.015f)
                    throw new InvalidDataException("HD footprint or height still differs "+path);
                row["after"]=Dimensions(actual);
                if(!PrefabUtility.SaveAsPrefabAsset(root,path))throw new IOException("Could not save "+path);
                changed++;
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        audit["adjustedFootprints"]=changed;
        File.WriteAllText(Report,audit.ToString());
        Debug.Log("CF_REFORGED_FOOTPRINT_AUDIT adjusted="+changed);
    }

    static Vector3 Size(JArray bounds)=>new Vector3((float)bounds[0],(float)bounds[1],(float)bounds[2]);
    static void LimitFootprint(Transform fit,Vector3 reference,Vector3 current){
        var scale=fit.localScale;
        if(current.x>reference.x*1.15f)scale.x*=reference.x*1.05f/current.x;
        if(current.z>reference.z*1.15f)scale.z*=reference.z*1.05f/current.z;
        fit.localScale=scale;
    }

    static void CaptureVortexIcon() {
        var data = JObject.Parse(File.ReadAllText(Vortex + ".json"));
        var atlas = new Texture2D(2, 2);
        Texture2D icon = null;
        try {
            if (!atlas.LoadImage(File.ReadAllBytes(Vortex + ".png"))) throw new InvalidDataException("Invalid Reforged vortex atlas");
            int columns = (int)data["Columns"], rows = (int)data["Rows"];
            int size = atlas.width / columns;
            if (atlas.height / rows != size) throw new InvalidDataException("Vortex atlas frames are not square");
            var pixels = atlas.GetPixels((columns / 2) * size, (rows / 2 - 1) * size, size, size);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                int i = y * size + x;
                pixels[i] = x < 2 || y < 2 || x >= size - 2 || y >= size - 2
                    ? new Color(.53f, .43f, .25f)
                    : new Color(.035f + pixels[i].r, .05f + pixels[i].g, .055f + pixels[i].b);
            }
            icon = new Texture2D(size, size, TextureFormat.RGB24, false);
            icon.SetPixels(pixels);
            icon.Apply();
            File.WriteAllBytes(IconRoot + "n02E.png", icon.EncodeToPNG());
        } finally {
            if (icon) Object.DestroyImmediate(icon);
            Object.DestroyImmediate(atlas);
        }
    }

    static Bounds MeasureClassic(string raw, JObject profiles) {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPC/" + raw + "-t0.prefab");
        var view = prefab ? prefab.GetComponent<PcModel>() : null;
        if (!view || view.raw != raw || view.reforged || view.particleOnly)
            throw new InvalidDataException("Missing classic prefab " + raw);
        return MeasurePrefab(prefab, profiles);
    }

    static Bounds MeasurePrefab(GameObject prefab, JObject profiles) {
        var root = Object.Instantiate(prefab);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try { return Measure(root, profiles); }
        finally { Object.DestroyImmediate(root); }
    }

    static Bounds MeasureCastle(GameObject prefab) {
        if(!prefab)throw new InvalidDataException("Missing classic castle prefab");
        var castle=Object.Instantiate(prefab);
        var mesh=new Mesh();
        var bounds = new Bounds();
        bool found = false;
        try {
            foreach (var skin in castle.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                skin.BakeMesh(mesh,true);
                foreach(Vector3 vertex in mesh.vertices)
                    Add(castle.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex)),ref bounds,ref found);
            }
        } finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(castle);}
        if (!found || bounds.size.y < .01f) throw new InvalidDataException("Missing classic castle geometry");
        return bounds;
    }

    static Bounds Measure(GameObject root, JObject profiles) {
        var bounds = new Bounds();
        var effectBounds = new Bounds();
        bool found = false, foundEffect = false;
        var mesh = new Mesh();
        try {
            foreach (var view in root.GetComponentsInChildren<PcModel>(true)) {
                if (view.particleOnly || !view.animatedRoot) continue;
                var clip = view.StandClip() ?? throw new InvalidDataException("Missing Stand clip " + view.raw);
                float sample = Mathf.Min(.15f, clip.length * .5f);
                clip.SampleAnimation(view.animatedRoot, sample);
                string key = string.IsNullOrEmpty(view.appearanceKey) ? view.raw : view.appearanceKey;
                var profile = profiles[key] ?? throw new InvalidDataException("Missing model appearance " + key);
                // NativeAppearance uses a zero interval for a legacy animation alias.
                var span = (JArray)profile["clips"][clip.name] ?? new JArray(0, 0);
                foreach (var skin in view.animatedRoot.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled)) {
                    skin.BakeMesh(mesh, true);
                    var vertices = mesh.vertices;
                    for (int part = 0; part < mesh.subMeshCount; part++) {
                        var surface = profile["surfaces"][part];
                        if (NativeAppearance.Sample((JObject)surface["geoset"], span, sample * 1000) *
                            NativeAppearance.Sample((JObject)surface["layer"], span, sample * 1000) < .01f) continue;
                        bool effect = (string)surface["blend"] == "additive";
                        foreach (int index in mesh.GetTriangles(part)) {
                            Vector3 world = skin.transform.TransformPoint(vertices[index]);
                            Vector3 point = root.transform.InverseTransformPoint(world);
                            if (effect) Add(point, ref effectBounds, ref foundEffect);
                            else Add(point, ref bounds, ref found);
                        }
                    }
                }
            }
        } finally { Object.DestroyImmediate(mesh); }
        if (!found && foundEffect) {
            bounds = effectBounds;
            found = true;
        }
        if (!found || !(bounds.size.y >= .01f) || float.IsInfinity(bounds.size.y))
            throw new InvalidDataException("No measurable visible Stand geometry");
        return bounds;
    }

    static void Add(Vector3 point, ref Bounds bounds, ref bool found) {
        if (found) bounds.Encapsulate(point);
        else { bounds = new Bounds(point, Vector3.zero); found = true; }
    }

    static JArray Dimensions(Bounds value) => new JArray(value.size.x, value.size.y, value.size.z, value.min.y);
}
