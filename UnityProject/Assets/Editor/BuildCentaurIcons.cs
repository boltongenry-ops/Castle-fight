using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using CastleFight;
// Actual model thumbnails: building art in the build menu, separate fighter portraits.
public static class BuildCentaurIcons {
    const int Size=256;
    static Color[] colors={new Color(.25f,.32f,.37f),new Color(.3f,.2f,.16f),new Color(.25f,.16f,.12f),new Color(.13f,.25f,.23f),new Color(.35f,.24f,.13f),new Color(.25f,.17f,.31f),new Color(.22f,.23f,.14f),new Color(.18f,.22f,.31f),new Color(.19f,.28f,.14f),new Color(.37f,.27f,.11f),new Color(.32f,.22f,.18f)};
    public static Texture2D Capture(GameObject prefab,JObject profile,int family,bool upgrade,bool actor){
        var obj=UnityEngine.Object.Instantiate(prefab);obj.transform.position=new Vector3(12000,0,12000);obj.transform.rotation=Quaternion.Euler(0,actor?150:0,0);
        foreach(var tr in obj.GetComponentsInChildren<Transform>())tr.gameObject.layer=30;
        var model=obj.GetComponent<PcModel>();
        var stand=model.reforged?model.StandClip():model.clips.First(c=>c.name=="Stand"||c.name.StartsWith("Stand"));
        float time=Mathf.Min(.2f,stand.length*.5f);
        stand.SampleAnimation(model.animatedRoot,time);
        var surfaces=(JArray)profile["surfaces"];var interval=(JArray)profile["clips"][stand.name]??new JArray(0,0);var skin=obj.GetComponentInChildren<SkinnedMeshRenderer>();
        bool effectPortrait=model.reforged&&surfaces.All(s=>(string)s["blend"]=="additive");
        var baked=new Mesh();skin.BakeMesh(baked,model.reforged&&model.godotHeight>0);var vertices=baked.vertices;Bounds bounds=new Bounds();bool found=false;
        for(int s=0;s<baked.subMeshCount;s++){
            float alpha=NativeAppearance.Sample((JObject)surfaces[s]["geoset"],interval,time*1000)*NativeAppearance.Sample((JObject)surfaces[s]["layer"],interval,time*1000);
            var block=new MaterialPropertyBlock();block.SetFloat("_NativeAlpha",alpha);skin.SetPropertyBlock(block,s);if(alpha<.01f||!effectPortrait&&(string)surfaces[s]["blend"]=="additive")continue;
            foreach(int index in baked.GetTriangles(s)){var point=model.reforged?skin.transform.TransformPoint(vertices[index]):skin.transform.position+skin.transform.rotation*vertices[index];if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);}
        }
        if(!found)throw new Exception("No visible icon geometry "+model.raw);
        if(actor&&model.HeadBounds(out var head)&&head.size.magnitude>.1f){bounds=head;bounds.Expand(new Vector3(head.size.y*.6f,head.size.y*.65f,head.size.y*.2f));}
        var cameraObj=new GameObject("Centaur icon camera");var cam=cameraObj.AddComponent<Camera>();cam.cullingMask=1<<30;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=colors[Mathf.Clamp(family,0,colors.Length-1)]*.32f;cam.backgroundColor=new Color(cam.backgroundColor.r,cam.backgroundColor.g,cam.backgroundColor.b,1);
        var direction=actor?new Vector3(.18f,.1f,-1).normalized:new Vector3(.75f,.85f,-1).normalized;
        cam.transform.position=bounds.center+direction*40;cam.transform.LookAt(bounds.center);cam.nearClipPlane=.01f;cam.farClipPlane=120;
        var right=cam.transform.right;var up=cam.transform.up;float extent=0;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=Vector3.Scale(bounds.extents,new Vector3(x,y,z));extent=Mathf.Max(extent,Mathf.Abs(Vector3.Dot(p,right)),Mathf.Abs(Vector3.Dot(p,up)));}cam.orthographicSize=Mathf.Max(.2f,extent*1.13f);
        var lightObj=new GameObject("Icon light");var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,-35,0);
        var ambient=RenderSettings.ambientLight;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);var rt=new RenderTexture(Size,Size,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(Size,Size,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Size,Size),0,0);image.Apply();RenderTexture.active=previous;RenderSettings.ambientLight=ambient;
        cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObj);UnityEngine.Object.DestroyImmediate(lightObj);UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(baked);
        Frame(image,upgrade);return image;
    }
    static void Frame(Texture2D image,bool upgrade){
        var pixels=image.GetPixels();var gold=upgrade?new Color(.92f,.72f,.26f):new Color(.53f,.43f,.25f);
        for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)if(x<5||y<5||x>=Size-5||y>=Size-5)pixels[y*Size+x]=gold;
        if(upgrade)for(int n=0;n<2;n++)for(int y=0;y<18;y++)for(int x=-10;x<=10;x++)if(Mathf.Abs(x)<10-y/2){int px=Size-22-n*23+x,py=Size-22+y/2;pixels[py*Size+px]=gold;}
        image.SetPixels(pixels);image.Apply();
    }
    static void Badge(Texture2D target,Texture2D badge){
        var pixels=target.GetPixels();int left=10,bottom=Size-78;
        for(int y=0;y<68;y++)for(int x=0;x<68;x++){int dx=left+x,dy=bottom+y;pixels[dy*Size+dx]=badge.GetPixelBilinear(x/67f,y/67f);}
        target.SetPixels(pixels);target.Apply();
    }
    static void Save(Texture2D t,string name){File.WriteAllBytes("Assets/SourceAssets/pcui/"+name+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
    public static void FinishPortraits(){
        foreach(string raw in new[]{"cU05","cU15","cU06","cU16"}){
            string path="Assets/SourceAssets/pcui/CentaurUnit-"+raw+".png";if(!File.Exists(path))continue;
            var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));var badge=new Texture2D(2,2);badge.LoadImage(File.ReadAllBytes("Assets/SourceAssets/pcui/Ability-"+(raw=="cU05"||raw=="cU15"?"CT04":"CT05")+".png"));Badge(image,badge);UnityEngine.Object.DestroyImmediate(badge);Save(image,"CentaurUnit-"+raw);
        }AssetDatabase.Refresh();
    }
    public static void PrepareAndRender(){
        BuildCentaurs.Prepare();RenderExisting();
    }
    public static void RenderExisting(){
        var pack=AssetDatabase.LoadAssetAtPath<PcRacePack>("Assets/Resources/CentaurKhanates.asset");var catalog=JObject.Parse(pack.catalog.text);var rows=(JArray)catalog["lines"];var profiles=(JObject)catalog["centaurAppearance"];
        int family=-1;for(int i=0;i<rows.Count;i++){var row=rows[i];if((bool)row["base"])family++;string actor=(string)row["unit"];if(string.IsNullOrEmpty(actor))continue;
            var portrait=Capture(pack.units[i],(JObject)profiles[actor],family,!(bool)row["base"],true);Save(portrait,"CentaurUnit-"+actor);
        }
        family=-1;for(int i=0;i<rows.Count;i++){var row=rows[i];if((bool)row["base"])family++;string raw=(string)row["building"],actor=(string)row["unit"];
            var image=Capture(pack.buildings[i],(JObject)profiles[raw],family,!(bool)row["base"],false);
            if(!string.IsNullOrEmpty(actor)){
                string badgeFile=actor=="cU05"||actor=="cU15"?"Ability-CT04":actor=="cU06"||actor=="cU16"?"Ability-CT05":"CentaurUnit-"+actor;
                var badge=new Texture2D(2,2);badge.LoadImage(File.ReadAllBytes("Assets/SourceAssets/pcui/"+badgeFile+".png"));Badge(image,badge);UnityEngine.Object.DestroyImmediate(badge);
            }Save(image,"Building-"+raw);
        }
        FinishPortraits();AssetDatabase.Refresh();for(int i=0;i<rows.Count;i++)pack.buildingIcons[i]=BuildPC.Texture("Building-"+(string)rows[i]["building"]);
        var lookup=JObject.Parse(File.ReadAllText("Assets/Centaur028/index.json"));foreach(var row in rows){string actor=(string)row["unit"];if(!string.IsNullOrEmpty(actor))lookup["CentaurUnitIcon/"+actor]="Assets/SourceAssets/pcui/CentaurUnit-"+actor+".png";}
        File.WriteAllText("Assets/Centaur028/index.json",lookup.ToString());EditorUtility.SetDirty(pack);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("CF_CENTAUR_ICONS_PASS buildings="+rows.Count);
    }
}
