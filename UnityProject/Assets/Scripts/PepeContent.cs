using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CastleFight {
public static class PepeContent {
    public const int Race = 16;
    public const string Pack = "PepeBrotherhood";
    internal sealed class Presentation {public PcRacePack race;}
    static readonly ConditionalWeakTable<PcGame,Presentation> avatars = new ConditionalWeakTable<PcGame,Presentation>();
    internal static Presentation For(PcGame game) => avatars.GetOrCreateValue(game);
    static AssetBundle bundle;
    static readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
    public static Texture2D FighterIcon(string actor) {
        if (!portraits.TryGetValue(actor, out var icon) || !icon)
            portraits[actor]=icon=Load<Texture2D>("Pepe/"+actor);
        return icon;
    }
    public static bool Available {get; private set;}

    public static IEnumerator Open() {
        if (!MobileContent.Active && !bundle) {
            string path = MobileContent.Media("Frontend/pepe.windows");
            if (File.Exists(path)) {
                var request = AssetBundle.LoadFromFileAsync(path);
                yield return request;
                bundle = request.assetBundle;
                if (!bundle) Debug.LogWarning("CF_PEPE_ART_UNAVAILABLE");
            }
        }
        Available = Load<TextAsset>("pc-pepe") != null;
    }

    public static T Load<T>(string key) where T : UnityEngine.Object {
        if (key == "MobileCatalogs/16") key = "pc-pepe";
        else if (key == "MobileBuilders/16") key = "Pepe/Builder";
        else if (key == "RaceIcon-pepe" || key.StartsWith("Aspect-pepe_", StringComparison.Ordinal))
            key = "Pepe/" + key;
        if (key != Pack && key != "pc-pepe" && !key.StartsWith("Pepe/", StringComparison.Ordinal)) return null;
        return bundle ? bundle.LoadAsset<T>(key) : Resources.Load<T>(key);
    }
}
}
