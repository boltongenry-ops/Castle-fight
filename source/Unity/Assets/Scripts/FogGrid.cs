using System;
using UnityEngine;
namespace CastleFight {
// Same 256x128 field, map extent, edge falloff and explored level as Godot fog_of_war.gd.
public sealed class FogGrid {
    public const int Width=256,Height=128;public readonly byte[] Current=new byte[Width*Height],Explored=new byte[Width*Height];
    public int Index(Vector2 point){float x=(point.x*30+8192)/16384,y=(point.y*30+4096)/8192;return x<0||x>=1||y<0||y>=1?-1:(int)(y*Height)*Width+(int)(x*Width);}
    public bool Visible(Vector2 point){int i=Index(point);return i>=0&&Current[i]>100;}
    public bool Known(Vector2 point){int i=Index(point);return i>=0&&Explored[i]>0;}
    public byte Display(Vector2 point){int i=Index(point);return i<0?(byte)0:(byte)Math.Max(Current[i],Explored[i]>0?85:0);}
    public float Sample(Vector2 point){float x=(point.x*30+8192)/64-.5f,y=(point.y*30+4096)/64-.5f;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);return Mathf.Lerp(Mathf.Lerp(At(ix,iy),At(ix+1,iy),x-ix),Mathf.Lerp(At(ix,iy+1),At(ix+1,iy+1),x-ix),y-iy)/255;}
    float At(int x,int y){if(x<0||y<0||x>=Width||y>=Height)return 0;int i=y*Width+x;return Math.Max(Current[i],Explored[i]>0?85:0);}
    public void Clear(){Array.Clear(Current,0,Current.Length);}
    public void RevealBase(){for(int y=0;y<Height;y++){float py=-4096+(y+.5f)*8192/Height;if(Mathf.Abs(py)>2016)continue;for(int x=0;x<Width;x++){float px=-8192+(x+.5f)*16384/Width;if(px>=-6144&&px<=-2016)Current[y*Width+x]=255;}}}
    public void Reveal(Vector2 point,float radius){float px=(point.x*30+8192)/64,py=(point.y*30+4096)/64,r=radius*30/64;for(int y=Mathf.Max(0,(int)(py-r-1));y<=Mathf.Min(Height-1,(int)(py+r+1));y++)for(int x=Mathf.Max(0,(int)(px-r-1));x<=Mathf.Min(Width-1,(int)(px+r+1));x++){float d=new Vector2(x+.5f-px,y+.5f-py).magnitude;if(d>r)continue;int index=y*Width+x;Current[index]=(byte)Mathf.Max(Current[index],Mathf.Clamp((int)((r-d)*255),0,255));}}
    public void Remember(){for(int i=0;i<Current.Length;i++)if(Current[i]>0)Explored[i]=255;}
}
}
