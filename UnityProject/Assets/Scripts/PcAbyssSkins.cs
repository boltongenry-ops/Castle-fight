using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const int AbyssSkinCount=8;
    Texture2D[] abyssSkins;
    bool abyssSkinsTried;
    readonly Dictionary<(Material,int),Material> abyssSkinMaterials=new Dictionary<(Material,int),Material>();

    static int AbyssSkinIndex(string raw){
        switch(raw){
            case "n00I":case "n00J":case "n00K":return 0;
            case "n002":case "n00X":return 1;
            case "n00H":return 2;
            case "n004":return 3;
            case "n007":return 4;
            case "n00S":return 5;
            case "n01P":case "n01Q":case "n01R":return 6;
            case "n00W":return 7;
            default:return -1;
        }
    }

    void LoadAbyssSkins(){
        if(abyssSkinsTried)return;
        abyssSkinsTried=true;
        var asset=Resources.Load<TextAsset>("WinterBattle/AbyssMutations");
        string path=MobileContent.Media("Frontend/AbyssMutations.bin");
        byte[] data=asset?asset.bytes:File.Exists(path)?File.ReadAllBytes(path):null;
        if(data==null||data.Length<17)return;
        var sheet=new Texture2D(2,2,TextureFormat.RGB24,false);
        try{
            using(var reader=new BinaryReader(new MemoryStream(data,false))){
                if(reader.ReadUInt32()!=0x31534241)return;
                int edge=reader.ReadInt32(),colorLength=reader.ReadInt32();
                if(edge<64||edge>256||colorLength<=0||colorLength>data.Length-13)return;
                if(!sheet.LoadImage(reader.ReadBytes(colorLength),false)||sheet.width!=4*edge||sheet.height!=2*edge)return;
                var masks=new byte[sheet.width*sheet.height];
                using(var alpha=new DeflateStream(reader.BaseStream,CompressionMode.Decompress,true)){
                    int offset=0;
                    while(offset<masks.Length){
                        int count=alpha.Read(masks,offset,masks.Length-offset);
                        if(count==0)return;
                        offset+=count;
                    }
                    if(alpha.ReadByte()!=-1)return;
                }
                var colors=sheet.GetPixels32();
                abyssSkins=new Texture2D[AbyssSkinCount];
                for(int tile=0;tile<abyssSkins.Length;tile++){
                    var pixels=new Color32[edge*edge];
                    int sx=tile%4*edge,sy=(1-tile/4)*edge;
                    for(int y=0;y<edge;y++)for(int x=0;x<edge;x++){
                        int id=(sy+y)*sheet.width+sx+x;
                        var color=colors[id];color.a=masks[id];
                        pixels[y*edge+x]=color;
                    }
                    var skin=new Texture2D(edge,edge,TextureFormat.RGBA32,true){
                        name="Abyss skin "+tile,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear
                    };
                    skin.SetPixels32(pixels);
                    skin.Apply(true,true);
                    abyssSkins[tile]=skin;
                }
            }
        }catch(InvalidDataException){Debug.LogWarning("Invalid Abyss mutation atlas");}
        finally{Destroy(sheet);}
    }

    bool ApplyAbyssSkin(PcModel view){
        if(view.reforged)return false;
        int tile=AbyssSkinIndex(view.raw);
        if(tile<0)return false;
        LoadAbyssSkins();
        if(abyssSkins==null||!abyssSkins[tile])return false;
        bool dressed=false;
        foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var materials=skin.sharedMaterials;
            if(materials.Length==0)continue;
            var body=materials[0]?materials[0].mainTexture:null;
            for(int i=0;i<materials.Length;i++){
                var material=materials[i];
                if(!material||!body||material.shader.name!="CastleFight/NativeUnit"||
                    material.mainTexture!=body&&tile!=6)continue;
                var key=(material,tile);
                if(!abyssSkinMaterials.TryGetValue(key,out var replacement)){
                    replacement=new Material(material){name=material.name+" Abyss",mainTexture=abyssSkins[tile]};
                    replacement.SetFloat("_HDPresent",0);
                    abyssSkinMaterials.Add(key,replacement);
                }
                materials[i]=replacement;
                dressed=true;
            }
            skin.sharedMaterials=materials;
        }
        return dressed;
    }

    void DisposeAbyssSkins(){
        foreach(var material in abyssSkinMaterials.Values)Destroy(material);
        abyssSkinMaterials.Clear();
        if(abyssSkins!=null)foreach(var skin in abyssSkins)if(skin)Destroy(skin);
        abyssSkins=null;
        abyssSkinsTried=false;
    }
}
}
