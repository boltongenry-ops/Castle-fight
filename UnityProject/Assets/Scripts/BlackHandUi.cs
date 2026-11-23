using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
internal static class BlackHandUi {
    static readonly string[] statusIds={"BHW1","BHW2","BHPs","BHCr","BHTx","BHJu","BHSl"};
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    static Texture2D[] skills, fighters;

    public static Texture2D JudgmentSigil()=>Texture("JudgmentSigil");
    public static Texture2D BuilderIcon()=>Texture("BuilderIcon");
    public static Texture2D BuilderSkin()=>Texture("BuilderSkin");
    public static Texture2D Emblem()=>Texture("Emblem");
    public static Texture2D Versus()=>Texture("Versus");
    public static Texture2D Aspect(int index)=>Texture(index==0?"AspectIron":"AspectSiege");

    public static Texture2D Ability(string id) {
        if(id=="BHBz")return Texture("Berserk");
        if(id=="BHBr")return Fighter("bU04");
        if(id=="BHKn")return Ability("BH15");
        if(id=="BHIr")return Aspect(0);
        if(string.IsNullOrEmpty(id)||id.Length!=4||id[0]!='B'||id[1]!='H')return null;
        int index;
        if(char.IsDigit(id[2])&&char.IsDigit(id[3]))index=(id[2]-'0')*10+id[3]-'0';
        else {
            int status=System.Array.IndexOf(statusIds,id);
            if(status<0)return null;
            index=19+status;
        }
        if(index>=26)return null;
        if(skills==null)skills=Atlas("SkillIcons",7,4,96,26);
        return skills?[index];
    }

    public static Texture2D Fighter(string actor) {
        if(string.IsNullOrEmpty(actor)||actor.Length!=4||actor[0]!='b'||actor[1]!='U'||
           !char.IsDigit(actor[2])||!char.IsDigit(actor[3]))return null;
        int index=(actor[2]-'0')*10+actor[3]-'0';
        if(index>=15)return null;
        if(fighters==null)fighters=Atlas("FighterIcons",5,3,128,15);
        return fighters?[index];
    }

    static Texture2D Texture(string key) {
        if(textures.TryGetValue(key,out var result))return result;
        result=Load(key,true);
        textures[key]=result;
        return result;
    }

    static Texture2D Load(string key,bool readOnly) {
        byte[] bytes=BlackHandArt.ReadAsset(key);
        if(bytes==null)return null;
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false){name="Black Hand "+key};
        if(image.LoadImage(bytes,readOnly)){image.wrapMode=TextureWrapMode.Clamp;return image;}
        Object.Destroy(image);
        return null;
    }

    static Texture2D[] Atlas(string key,int columns,int rows,int size,int count) {
        var source=Load(key,false);
        if(!source)return null;
        var icons=new Texture2D[count];
        try {
            if(source.width!=size*columns||source.height!=size*rows)return null;
            for(int index=0;index<count;index++){
                var icon=new Texture2D(size,size,TextureFormat.RGBA32,false){name=key+" "+index,wrapMode=TextureWrapMode.Clamp};
                icon.SetPixels(source.GetPixels(index%columns*size,(rows-1-index/columns)*size,size,size));
                icon.Apply(false,true);
                icons[index]=icon;
            }
            return icons;
        } finally {Object.Destroy(source);}
    }
}
}
