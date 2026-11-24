using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CastleFight {
sealed class CastleThemes
{
    public const int RaceCount = 18;
    const int AtlasSize = 512;
    static readonly string[] Names = {
        "human", "orc", "naga", "nature", "chaos", "northern", "mechanical", "undead", "corrupted",
        "elementals", "nightelf", "elf", "builder", "nerubian", "pandaren", "centaur", "pepe", "blackhand"
    };

    sealed class Side
    {
        public GameObject castle;
        public int race = -1;
        public Texture2D skin;
        public Texture source;
        public readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        public readonly Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();

        public void Clear()
        {
            foreach (var original in originals)
                if (original.Key) original.Key.sharedMaterials = original.Value;
            foreach (var material in replacements.Values) Object.Destroy(material);
            if (skin) Object.Destroy(skin);
            originals.Clear();
            replacements.Clear();
            castle = null;
            skin = null;
            source = null;
            race = -1;
        }
    }

    readonly Side[] sides = { new Side(), new Side() };

    public bool NeedsApply(GameObject castle, int race, int team) =>
        castle && (uint)race < RaceCount && (sides[team].castle != castle || sides[team].race != race);

    public static string RaceName(int race) => Names[race];

    public void Apply(GameObject castle, int race, int team, bool reforged)
    {
        if (!NeedsApply(castle, race, team)) return;
        var side = sides[team];
        side.Clear();
        side.castle = castle;
        side.race = race;
        // The generated artwork is authored for the classic mesh UVs.
        if (reforged) return;
        var art = LoadAtlas(Names[race], out bool owned);
        if (!art) return;
        foreach (var renderer in castle.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
            var original = renderer.sharedMaterials;
            Material[] themed = null;
            for (int i = 0; i < original.Length; i++)
            {
                var material = original[i];
                if (!material || material.shader.name != "CastleFight/NativeMapProp" ||
                    !(material.mainTexture is Texture2D source) || source.width != AtlasSize || source.height != AtlasSize)
                    continue;
                if (!side.skin)
                {
                    side.skin = PreserveTeamMask(source, art, race);
                    side.source = source;
                }
                if (source != side.source) continue;
                if (!side.replacements.TryGetValue(material, out var themedMaterial))
                {
                    themedMaterial = new Material(material) { mainTexture = side.skin };
                    side.replacements.Add(material, themedMaterial);
                }
                if (themed == null) themed = (Material[])original.Clone();
                themed[i] = themedMaterial;
            }
            if (themed == null) continue;
            side.originals.Add(renderer, original);
            renderer.sharedMaterials = themed;
        }
        if (owned) Object.Destroy(art);
    }

    static Texture2D LoadAtlas(string name, out bool owned)
    {
        var texture = Resources.Load<Texture2D>("CastleThemes/" + name);
        owned = !texture;
        if (!owned) return texture;
        string path = Path.Combine(Application.streamingAssetsPath, "Frontend", "CastleThemes", name + ".jpg");
        if (!File.Exists(path)) return null;
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (texture.LoadImage(File.ReadAllBytes(path), true)) return texture;
        Object.Destroy(texture);
        return null;
    }

    public static Texture2D Readback(Texture source, bool mipmaps)
    {
        var rt = RenderTexture.GetTemporary(AtlasSize, AtlasSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var result = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, mipmaps);
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, AtlasSize, AtlasSize), 0, 0, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
        return result;
    }

    static Texture2D PreserveTeamMask(Texture source, Texture2D art, int race)
    {
        var teamMask = Readback(source, false);
        var texture = Readback(art, true);
        var original = teamMask.GetPixels32();
        var pixels = texture.GetPixels32();
        Object.Destroy(teamMask);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].a = original[i].a;
        texture.name = "Faction atlas " + race + " · " + source.name;
        texture.wrapMode = source.wrapMode;
        texture.filterMode = source.filterMode;
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    public void Clear()
    {
        foreach (var side in sides) side.Clear();
    }
}
}
