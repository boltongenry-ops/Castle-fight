using System;
using System.Collections;
using System.Collections.Generic;
using GLTFast;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
internal static class WardrobeCatalog {
    const string ResourceRoot="Wardrobe/";

    internal static byte[] Bytes(string name){
        var embedded=Resources.Load<TextAsset>(ResourceRoot+name);
        if(embedded){
            var bytes=embedded.bytes;
            Resources.UnloadAsset(embedded);
            return bytes;
        }
        if(Application.isMobilePlatform) return null;
        var path=MobileContent.Media("Frontend/Wardrobe/"+name+".bytes");
        return File.Exists(path)?File.ReadAllBytes(path):null;
    }

    internal static void Extend(JArray items){
        var embedded=Resources.Load<TextAsset>(ResourceRoot+"Catalog");
        var path=MobileContent.Media("Frontend/Wardrobe/Catalog.json");
        var text=embedded?embedded.text:!Application.isMobilePlatform&&File.Exists(path)?File.ReadAllText(path):null;
        if(text==null) return;
        foreach(var row in JObject.Parse(text)["items"].OfType<JObject>()) items.Add(row.DeepClone());
    }

    internal static bool IsWardrobeProduct(JObject row)=>row?["role"]!=null;

    internal static IEnumerable<string> Pieces(JObject row){
        if(row?["includes"] is JArray includes)
            foreach(var value in includes) yield return (string)value;
    }

    internal static int Price(JObject row,Func<string,bool> owns,Func<string,JObject> find){
        int price=(int)row["cost"];
        if(!(row["includes"] is JArray)) return price;
        int missing=Pieces(row).Where(id=>!owns(id)).Sum(id=>(int)find(id)["cost"]);
        return Math.Min(price,missing);
    }

    internal static string Rarity(JObject row){
        switch((string)row["rarity"]){
            case "legendary":return "Легендарный";
            case "epic":return "Эпический";
            default:return "Редкий";
        }
    }
}

public sealed partial class PcGame {
    sealed class WardrobeArt {
        public readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public readonly Dictionary<(Material,string),Material> materials=new Dictionary<(Material,string),Material>();
        public GameObject moonleaf;
        public GltfImport moonleafImport;
        public Material[] moonleafMaterials;
    }

    static readonly ConditionalWeakTable<PcGame,WardrobeArt> wardrobeArt=new ConditionalWeakTable<PcGame,WardrobeArt>();
    WardrobeArt OutfitArt=>wardrobeArt.GetValue(this,_=>new WardrobeArt());

    int WorkshopPrice(JObject row)=>WardrobeCatalog.Price(row,AccountOwns,progression.Item);

    bool WorkshopEquipped(JObject row){
        string id=(string)row["id"];
        if(!AccountOwns(id)) return false;
        if(row["includes"] is JArray)
            return WardrobeCatalog.Pieces(row).All(piece=>AccountEquipment((string)progression.Item(piece)["kind"])==piece);
        return AccountEquipment((string)row["kind"])==id;
    }

    Texture2D OutfitTexture(string name,bool atlas){
        var art=OutfitArt;
        if(art.textures.TryGetValue(name,out var texture)) return texture;
        try {
            var payload=WardrobeCatalog.Bytes(name);
            if(payload!=null){
                texture=new Texture2D(2,2,TextureFormat.RGBA32,atlas){name=name,filterMode=atlas?FilterMode.Trilinear:FilterMode.Bilinear};
                if(!texture.LoadImage(payload,true)){Destroy(texture);texture=null;}
            }
        } catch(IOException error) {
            Debug.LogWarning("Wardrobe asset "+name+": "+error.Message);
        }
        art.textures[name]=texture;
        return texture;
    }

    void DrawOutfitImage(Rect frame,JObject row){
        string id=(string)row["id"];
        if(row["includes"] is JArray){
            var pieces=WardrobeCatalog.Pieces(row).ToArray();
            float width=frame.width*.49f;
            for(int i=0;i<pieces.Length;i++){
                var image=OutfitTexture(pieces[i]+"-preview",false);
                if(image) GUI.DrawTexture(new Rect(frame.x+i*(frame.width-width),frame.y,width,frame.height),image,ScaleMode.ScaleToFit,true);
            }
        }else{
            var image=OutfitTexture(id+"-preview",false);
            if(image) GUI.DrawTexture(frame,image,ScaleMode.ScaleToFit,true);
        }
    }

    JObject EquippedOutfit(int race,bool builder){
        if(progression==null) return null;
        string id=AccountEquipment("uniform_"+(builder?"builder":"unit")+"_"+race);
        return string.IsNullOrEmpty(id)||!AccountOwns(id)?null:progression.Item(id);
    }

    IEnumerator PrepareWardrobeModel(int race,int generation){
        if((string)EquippedOutfit(race,true)?["model"]=="moonleaf-artisan")
            yield return LoadMoonleafModel(generation);
    }

    IEnumerator LoadMoonleafModel(int generation){
        var art=OutfitArt;
        if(art.moonleaf) yield break;
        var data=WardrobeCatalog.Bytes("moonleaf-artisan");
        if(data==null) yield break;
        var job=ImportRitualActor(data,"pBld","Moonleaf Artisan",1.25f,2.7f,90,false);
        while(!job.IsCompleted) yield return null;
        if(job.IsFaulted){
            Debug.LogWarning("Moonleaf outfit: "+job.Exception.GetBaseException().Message);
            yield break;
        }
        if(generation!=versusSerial||art.moonleaf){
            var (unused,importer,materials)=job.Result;
            ReleaseRitualArt(unused,importer,materials);
            yield break;
        }
        (art.moonleaf,art.moonleafImport,art.moonleafMaterials)=job.Result;
    }

    GameObject WardrobeBuilder(int race,int team){
        if(team!=0||SpectatorSceneReady||(string)EquippedOutfit(race,true)?["model"]!="moonleaf-artisan") return null;
        return wardrobeArt.TryGetValue(this,out var art)?art.moonleaf:null;
    }

    void DressBattleActor(PcModel model,int team,bool builder){
        if(!model||SpectatorSceneReady||team!=0) return;
        var item=EquippedOutfit(matchRace,builder);
        if(item!=null&&item["model"]==null) ApplyDye(model,item);
    }

    bool ApplyDye(PcModel model,JObject item){
        if(!model||model.reforged||model.raw!=(string)item["raw"]) return false;
        string id=(string)item["id"],baseTexture=(string)item["baseTexture"];
        var atlas=OutfitTexture(id,true);
        if(!atlas) return false;
        var art=OutfitArt;
        bool changed=false;
        foreach(var render in model.GetComponentsInChildren<Renderer>(true)){
            bool updated=false;
            var slots=render.sharedMaterials;
            for(int i=0;i<slots.Length;i++){
                var original=slots[i];
                if(!original||!original.mainTexture||!original.HasProperty("_Team")||original.GetFloat("_Team")>.5f
                    ||original.GetInt("_DstBlend")!=0) continue;
                var originalTexture=original.mainTexture;
                if(baseTexture!=null?originalTexture.name!=baseTexture:originalTexture.width!=256||originalTexture.height!=256) continue;
                var key=(original,id);
                if(!art.materials.TryGetValue(key,out var costume)){
                    costume=new Material(original){name=id,mainTexture=atlas};
                    art.materials.Add(key,costume);
                }
                slots[i]=costume;
                updated=true;
            }
            if(updated){render.sharedMaterials=slots;changed=true;}
        }
        return changed;
    }

    void DisposeOutfits(){
        if(!wardrobeArt.TryGetValue(this,out var art)) return;
        ReleaseRitualArt(art.moonleaf,art.moonleafImport,art.moonleafMaterials);
        foreach(var material in art.materials.Values) if(material) Destroy(material);
        foreach(var texture in art.textures.Values) if(texture) Destroy(texture);
        wardrobeArt.Remove(this);
    }
}
}
