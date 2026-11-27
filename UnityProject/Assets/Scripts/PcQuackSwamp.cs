using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    readonly List<(MeshRenderer target, MaterialPropertyBlock original, MaterialPropertyBlock wet)> quackGround=
        new List<(MeshRenderer, MaterialPropertyBlock, MaterialPropertyBlock)>();
    readonly Dictionary<Texture,Texture2D> quackGroundTextures=new Dictionary<Texture,Texture2D>();
    GameObject quackWetland;
    Mesh quackWetlandMesh;
    Material quackWetlandMaterial;
    Texture2D quackWetlandAtlas;
    bool quackSwampRevealed;

    void PrepareQuackSwamp(){
        var terrainRoot=controls.sceneryRoot.transform.parent;
        foreach(Transform section in terrainRoot){
            if(!section.name.StartsWith("Terrain",StringComparison.Ordinal))continue;
            foreach(var renderer in section.GetComponentsInChildren<MeshRenderer>()){
                if(!renderer.name.StartsWith("Ground ",StringComparison.Ordinal)||!renderer.sharedMaterial)continue;
                var source=renderer.sharedMaterial.mainTexture;
                if(!source)continue;
                if(!quackGroundTextures.TryGetValue(source,out var wetTexture)){
                    wetTexture=MakeQuackWetGround(source);
                    quackGroundTextures.Add(source,wetTexture);
                }
                var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
                var wet=new MaterialPropertyBlock();renderer.GetPropertyBlock(wet);
                wet.SetTexture("_MainTex",wetTexture);
                quackGround.Add((renderer,original,wet));
            }
        }
        BuildQuackWetland();
    }

    void RevealQuackSwamp(){
        if(quackSwampRevealed)return;
        quackSwampRevealed=true;
        foreach(var entry in quackGround)
            if(entry.target)entry.target.SetPropertyBlock(entry.wet);
        if(quackWetland)quackWetland.SetActive(true);
    }

    static Texture2D MakeQuackWetGround(Texture source){
        float shrink=Mathf.Min(1,512f/Mathf.Max(source.width,source.height));
        int width=Mathf.Max(1,Mathf.RoundToInt(source.width*shrink));
        int height=Mathf.Max(1,Mathf.RoundToInt(source.height*shrink));
        var temporary=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var old=RenderTexture.active;
        var atlas=new Texture2D(width,height,TextureFormat.RGBA32,true){
            name="Земля болота",wrapMode=source.wrapMode,filterMode=source.filterMode
        };
        try{
            Graphics.Blit(source,temporary);
            RenderTexture.active=temporary;
            atlas.ReadPixels(new Rect(0,0,width,height),0,0,false);
        }finally{
            RenderTexture.active=old;RenderTexture.ReleaseTemporary(temporary);
        }
        var pixels=atlas.GetPixels32();
        for(int i=0;i<pixels.Length;i++){
            var pixel=pixels[i];
            float detail=(pixel.r*.213f+pixel.g*.715f+pixel.b*.072f)/255f;
            float grass=Mathf.Clamp01((pixel.g-pixel.r+13)/47f);
            var moss=new Color(.038f+detail*.40f,.09f+detail*.59f,.075f+detail*.39f,pixel.a/255f);
            var mud=new Color(.061f+detail*.57f,.063f+detail*.45f,.057f+detail*.36f,pixel.a/255f);
            pixels[i]=Color.Lerp(mud,moss,grass);
        }
        atlas.SetPixels32(pixels);
        atlas.Apply(true,true);
        return atlas;
    }

    void BuildQuackWetland(){
        var random=new System.Random(4913);
        var vertices=new List<Vector3>(512);
        var uv=new List<Vector2>(512);
        var indices=new List<int>(768);
        for(int i=0;i<46;i++){
            int column=i%12,row=i/12;
            float x=-187+column*34+(float)(random.NextDouble()-.5)*19;
            float y=17+row%2*28+(float)(random.NextDouble()-.5)*13;
            if(row<2)y=-y;
            var pool=new Vector2(x,y);
            if(!NativeMapTerrain.Contains(pool.x*30,pool.y*30))continue;
            float angle=(float)random.NextDouble()*360;
            float width=8+(float)random.NextDouble()*10;
            if(!QuackWetQuad(pool,width,4+width*.36f,angle,0,.07f,vertices,uv,indices))continue;
            for(int leaf=0;leaf<2;leaf++){
                var offset=new Vector2((leaf==0?-1:1)*width*.19f,leaf==0?-.9f:1.1f);
                QuackWetQuad(pool+offset,2.3f+leaf*.55f,2.4f,angle+leaf*80,leaf==0?1:2,.11f,vertices,uv,indices);
            }
        }
        for(int i=0;i<HumanMatch.QuackPoolCount;i++){
            var pool=HumanMatch.QuackPoolPoint(i);
            QuackWetQuad(pool,12,8,i*35,0,.09f,vertices,uv,indices);
            QuackWetQuad(pool+new Vector2(-2,-1.4f),2.8f,2.6f,i*70,2,.13f,vertices,uv,indices);
        }
        quackWetlandMesh=new Mesh{name="Лужи и кувшинки",vertices=vertices.ToArray(),uv=uv.ToArray(),triangles=indices.ToArray()};
        quackWetlandMesh.RecalculateBounds();
        quackWetlandAtlas=MakeQuackWetlandAtlas();
        var shader=Shader.Find("Sprites/Default");
        if(!shader)return;
        quackWetlandMaterial=new Material(shader){mainTexture=quackWetlandAtlas,renderQueue=2950};
        quackWetland=new GameObject("Болото Великого Квака");
        quackWetland.transform.SetParent(visuals.transform,false);
        quackWetland.AddComponent<MeshFilter>().sharedMesh=quackWetlandMesh;
        var renderer=quackWetland.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=quackWetlandMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        quackWetland.SetActive(false);
    }

    bool QuackWetQuad(Vector2 center,float width,float height,float angle,int tile,float lift,
        List<Vector3> vertices,List<Vector2> uv,List<int> indices){
        int first=vertices.Count;
        var axis=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*width*.5f;
        var perpendicular=new Vector2(-axis.y,axis.x)*(height/width);
        var corners=new[]{World(center-axis-perpendicular),World(center+axis-perpendicular),
            World(center+axis+perpendicular),World(center-axis+perpendicular)};
        float low=corners[0].y,high=corners[0].y;
        foreach(var corner in corners){low=Mathf.Min(low,corner.y);high=Mathf.Max(high,corner.y);}
        if(high-low>1.35f)return false;
        foreach(var corner in corners)vertices.Add(corner+Vector3.up*lift);
        float left=tile/4f+.002f,right=(tile+1)/4f-.002f;
        uv.Add(new Vector2(left,.012f));uv.Add(new Vector2(right,.012f));
        uv.Add(new Vector2(right,.988f));uv.Add(new Vector2(left,.988f));
        indices.Add(first);indices.Add(first+1);indices.Add(first+2);
        indices.Add(first);indices.Add(first+2);indices.Add(first+3);
        return true;
    }

    static Texture2D MakeQuackWetlandAtlas(){
        const int size=64, width=size*4;
        var pixels=new Color32[width*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            var p=new Vector2((x+.5f)/(size*.5f)-1,(y+.5f)/(size*.5f)-1);
            float r=p.magnitude,angle=Mathf.Atan2(p.y,p.x);
            float puddle=Mathf.Clamp01((1-r+Mathf.Sin(angle*5)*.055f)*7);
            float bands=Mathf.Pow(Mathf.Max(0,Mathf.Sin(r*24+Mathf.Sin(angle*3)*.8f)),10)*.19f;
            float ripple=bands*(1-Mathf.Clamp01(r));
            pixels[y*width+x]=new Color(.049f+ripple,.23f+ripple,.29f+ripple,.69f*puddle);
            float notch=Mathf.Abs(angle+.4f)<.18f&&r>.10f?0:1;
            float leaf=Mathf.Clamp01((.86f-r)*15)*notch;
            float veins=Mathf.Pow(Mathf.Abs(Mathf.Sin(angle*7+.3f)),9)*.08f;
            var green=new Color(.19f+veins,.53f+veins,.19f,.95f*leaf);
            pixels[y*width+x+size]=green;
            var blossom=(p-new Vector2(.13f,.13f))*2.8f;
            float petals=Mathf.Clamp01((.78f+.18f*Mathf.Cos(Mathf.Atan2(blossom.y,blossom.x)*7)-blossom.magnitude)*12);
            pixels[y*width+x+size*2]=Color.Lerp(green,new Color(1,.78f,.83f,1),petals);
        }
        var texture=new Texture2D(width,size,TextureFormat.RGBA32,false){name="Лужи и кувшинки",wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        return texture;
    }

    void ClearQuackSwamp(){
        foreach(var entry in quackGround)
            if(entry.target)entry.target.SetPropertyBlock(entry.original);
        quackGround.Clear();
        foreach(var texture in quackGroundTextures.Values)if(texture)Destroy(texture);
        quackGroundTextures.Clear();
        quackSwampRevealed=false;
        if(quackWetland){quackWetland.SetActive(false);Destroy(quackWetland);}
        if(quackWetlandMesh)Destroy(quackWetlandMesh);
        if(quackWetlandMaterial)Destroy(quackWetlandMaterial);
        if(quackWetlandAtlas)Destroy(quackWetlandAtlas);
        quackWetland=null;quackWetlandMesh=null;quackWetlandMaterial=null;quackWetlandAtlas=null;
    }
}
}
