using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Small platform supplement keeps the existing immutable content and PC data intact.
public static class CentaurContent {
    static AssetBundle bundle;static JObject index;
    public static IEnumerator Open(){
        if(bundle||MobileContent.Active)yield break;
        string path=MobileContent.Media("Frontend/centaur-028.windows");if(!File.Exists(path))yield break;
        var load=AssetBundle.LoadFromFileAsync(path);yield return load;bundle=load.assetBundle;
        if(!bundle)throw new InvalidDataException("Не удалось открыть ресурсы кентавров");
        index=JObject.Parse(bundle.LoadAsset<TextAsset>("Assets/Centaur028/index.json").text);
    }
    public static T Load<T>(string key) where T:UnityEngine.Object{
        string asset=(string)index?[key];return bundle&&asset!=null?bundle.LoadAsset<T>(asset):null;
    }
}
public sealed partial class PcGame {
    PcRacePack centaurPack;
    IEnumerator LoadCentaurArt(){
        if(!MobileContent.Active){yield return CentaurContent.Open();centaurPack=ContentResources.Load<PcRacePack>("CentaurKhanates");}
    }
    Texture2D CentaurUnitIcon(string raw)=>ContentResources.Load<Texture2D>("CentaurUnitIcon/"+raw);
}
}
