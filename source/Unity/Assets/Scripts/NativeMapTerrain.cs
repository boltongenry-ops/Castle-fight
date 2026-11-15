using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
// Map coordinates, triangulation and corner masks match Godot terrain_mesh.gd.
public sealed class NativeMapTerrain {
    public readonly int width,height,count; public readonly float originX,originY,tileSize,waterOffset;
    public readonly float[] ground,water,walk;public readonly byte[] textures,flags,variations;
    public readonly JObject waterConfig;
    public const float Scale=1f/30f;
    public NativeMapTerrain(byte[] bytes,JObject meta,JObject waterSettings) {
        width=BitConverter.ToInt32(bytes,0);height=BitConverter.ToInt32(bytes,4);count=width*height;
        if(width!=(int)meta["pointsX"]||height!=(int)meta["pointsY"]||bytes.Length<8+count*10)throw new FormatException("Invalid terrain header/length");
        originX=(float)meta["originX"];originY=(float)meta["originY"];tileSize=(float)meta["tileSize"];
        waterConfig=waterSettings;waterOffset=(float)waterSettings["offset_wc3"];
        ground=new float[count];water=new float[count];walk=new float[count];textures=new byte[count];flags=new byte[count];variations=new byte[count];
        Buffer.BlockCopy(bytes,8,ground,0,count*4);Buffer.BlockCopy(bytes,8+count*4,water,0,count*4);
        Buffer.BlockCopy(bytes,8+count*8,textures,0,count);Buffer.BlockCopy(bytes,8+count*9,flags,0,count);
        if(bytes.Length>=8+count*11)Buffer.BlockCopy(bytes,8+count*10,variations,0,count);
        Array.Copy(ground,walk,count);
        for(int y=0;y<height-1;y++)for(int x=0;x<width-1;x++) {
            int p=y*width+x;int[] c={p,p+1,p+width,p+width+1};
            if(c.Any(i=>(flags[i]&0x40)!=0))foreach(int i in c)walk[i]=Mathf.Max(walk[i],water[i]+waterOffset);
        }
    }
    public static bool Contains(float x,float y)=>x>=-6144&&x<=6144&&y>=-3584&&y<=3584;
    public bool ContainsTile(int x,int y)=>Contains(originX+x*tileSize,originY+y*tileSize)&&Contains(originX+(x+1)*tileSize,originY+(y+1)*tileSize);
    public Vector3 Position(float x,float y,bool walking=false)=>new Vector3(x*Scale,Height(x,y,walking),-y*Scale);
    public float Height(float x,float y,bool walking=false) {
        float fx=(x-originX)/tileSize,fy=(y-originY)/tileSize;int ix=Mathf.FloorToInt(fx),iy=Mathf.FloorToInt(fy);
        if(ix<0||iy<0||ix>=width-1||iy>=height-1)return 0;
        float tx=fx-ix,ty=fy-iy;var field=walking?walk:ground;int i=iy*width+ix;
        float a=field[i],b=field[i+1],c=field[i+width],d=field[i+width+1];
        return (tx>=ty?a+(b-a)*tx+(d-b)*ty:a+(d-c)*tx+(c-a)*ty)*Scale;
    }
    Vector3 Normal(int x,int y) {
        float l=ground[y*width+Mathf.Max(0,x-1)],r=ground[y*width+Mathf.Min(width-1,x+1)];
        float d=ground[Mathf.Max(0,y-1)*width+x],u=ground[Mathf.Min(height-1,y+1)*width+x];
        return new Vector3((l-r)/(2*tileSize),1,-(d-u)/(2*tileSize)).normalized;
    }
    public Mesh GroundMesh(int surface,Texture2D texture) {
        int g=surface%9;bool overlay=surface>=9;
        var pos=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var triangles=new List<int>();
        for(int y=0;y<height-1;y++)for(int x=0;x<width-1;x++) {
            if(!ContainsTile(x,y))continue;int p=y*width+x;int[] corners={p,p+1,p+width+1,p+width};
            int[] ids=corners.Select(i=>Mathf.Min(textures[i]&15,8)).ToArray();var layers=ids.Distinct().OrderBy(i=>i).ToArray();
            int rank=Array.IndexOf(layers,g);if(rank<0||(overlay?rank==0:rank!=0))continue;
            int cols=texture.width/64,rows=texture.height/64,v=variations[p],cx,cy;
            if(cols>rows){cx=v<16?4+v%4:v==16?3:0;cy=v<16?v/4:v==16?3:0;}else{cx=cy=v==0?0:3;}
            if(overlay){int mask=0;int[] bits={2,1,4,8};for(int k=0;k<4;k++)if(ids[k]==g)mask|=bits[k];cx=mask%4;cy=mask/4;}
            int first=pos.Count;
            for(int k=0;k<4;k++) {
                int dx=(k==1||k==2)?1:0,dy=k>=2?1:0;
                pos.Add(new Vector3((originX+(x+dx)*tileSize)*Scale,ground[corners[k]]*Scale+rank*.002f,-(originY+(y+dy)*tileSize)*Scale));
                normals.Add(Normal(x+dx,y+dy));
                uv.Add(new Vector2((cx*64+.5f+dx*63)/texture.width,1-(cy*64+.5f+(1-dy)*63)/texture.height));
            }
            triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
        }
        var mesh=new Mesh {name="Native ground "+surface,indexFormat=IndexFormat.UInt32};mesh.SetVertices(pos);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
    }
    public Mesh WaterMesh(out int tileCount) {
        var pos=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();tileCount=0;
        for(int y=0;y<height-1;y++)for(int x=0;x<width-1;x++) {
            if(!ContainsTile(x,y))continue;int p=y*width+x;int[] c={p,p+1,p+width+1,p+width};
            if(!c.Any(i=>(flags[i]&0x40)!=0&&water[i]+waterOffset>ground[i]))continue;
            int first=pos.Count;
            for(int k=0;k<4;k++) {
                int dx=(k==1||k==2)?1:0,dy=k>=2?1:0;float h=water[c[k]]+waterOffset;
                pos.Add(new Vector3((originX+(x+dx)*tileSize)*Scale,h*Scale,-(originY+(y+dy)*tileSize)*Scale));
                uv.Add(new Vector2(dx,dy));colors.Add(DepthColor(h-ground[c[k]]));
            }
            triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});tileCount++;
        }
        var mesh=new Mesh {name="Native ASha water",indexFormat=IndexFormat.UInt32};mesh.SetVertices(pos);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public Color DepthColor(float depth) {
        bool shallow=depth<=64;var a=(JArray)waterConfig[shallow?"shallow_min":"deep_min"];var b=(JArray)waterConfig[shallow?"shallow_max":"deep_max"];
        float t=Mathf.Clamp01(shallow?(depth-10)/54:(depth-64)/8);
        return Color.Lerp(new Color((float)a[0],(float)a[1],(float)a[2],(float)a[3]),new Color((float)b[0],(float)b[1],(float)b[2],(float)b[3]),t);
    }
}
}
