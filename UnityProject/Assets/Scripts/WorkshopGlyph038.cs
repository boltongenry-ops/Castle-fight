using UnityEngine;

namespace CastleFight {
internal static class WorkshopGlyph038 {
    const int Size=96;

    internal static Texture2D Create(string kind,int pattern,Color baseColor,Color highlight){
        var icon=new Texture2D(Size,Size,TextureFormat.RGBA32,false){
            name="Workshop pictogram "+kind,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear
        };
        var pixels=new Color32[Size*Size];
        for(int y=0;y<Size;y++)for(int x=0;x<Size;x++){
            float px=(x+.5f)/Size*2-1,py=(y+.5f)/Size*2-1;
            float radius=Mathf.Sqrt(px*px+py*py),angle=Mathf.Atan2(py,px);
            Color color;
            if(kind=="skin")color=Tabard(px,py,pattern,baseColor,highlight);
            else if(kind=="aura")color=Sigil(px,py,angle,pattern,baseColor,highlight);
            else if(kind=="effect")color=Ember(px,py,pattern,baseColor,highlight);
            else if(kind=="coin")color=Coin(px,py,radius,baseColor,highlight);
            else color=Frame(px,py,baseColor,highlight);
            pixels[y*Size+x]=color;
        }
        icon.SetPixels32(pixels);icon.Apply(false,true);
        return icon;
    }

    static float Stroke(float value,float width)=>Mathf.Clamp01((width+.025f-Mathf.Abs(value))/.025f);
    static Color Paint(Color ink,float alpha){ink.a=alpha;return ink;}
    static Color Sigil(float x,float y,float angle,int pattern,Color ink,Color light){
        float radial=Mathf.Sqrt(x*x+y*y*1.3f);
        float outer=.72f+.06f*Mathf.Cos(angle*(3+pattern%5));
        float rail=Stroke(radial-outer,.035f);
        float inside=Stroke(radial-.5f,.022f)*Mathf.Clamp01(Mathf.Cos(angle*(3+pattern%6))+.38f);
        float star=Mathf.Abs(x)+Mathf.Abs(y)*1.17f;
        float center=Stroke(star-.22f,.035f);
        float glow=Mathf.Exp(-Mathf.Abs(radial-outer)*13)*.29f;
        return Paint(Color.Lerp(ink,light,Mathf.Max(inside*.64f,center)),Mathf.Max(glow,Mathf.Max(rail,Mathf.Max(inside,center))));
    }

    static Color Tabard(float x,float y,int pattern,Color ink,Color light){
        float px=Mathf.Abs(x);
        float width=y>.28f?.56f-(y-.28f)*.72f:.48f-y*.17f;
        float neck=Mathf.Max(0,.22f-px)*.7f;
        float cloth=Mathf.Min(width-px,Mathf.Min(y+.78f,.74f-y-neck));
        float body=Mathf.SmoothStep(0,1,Mathf.Clamp01(cloth*34));
        float trim=Stroke(cloth,.045f);
        float seam=Stroke(x,.016f)*.38f;
        float shield=Stroke(Mathf.Abs(x)*(1.2f+pattern*.06f)+Mathf.Abs(y+.02f)-.22f,.033f);
        float diagonal=Stroke(y-x*(pattern%2==0?1:-1)-.15f,.035f)*.43f;
        float fabric=.54f+px*.16f+(Mathf.Sin(x*46)*.07f+.19f)*Mathf.Clamp01(y+.7f);
        Color color=Color.Lerp(ink*fabric,light,Mathf.Max(trim*.8f,Mathf.Max(shield,Mathf.Max(seam,diagonal))));
        return Paint(color,body);
    }

    static Color Ember(float x,float y,int pattern,Color ink,Color light){
        float spark=0,glow=0;
        for(int i=0;i<5;i++){
            float ox=(i-2)*.29f,oy=Mathf.Sin((i+pattern)*1.8f)*.36f+i*.038f;
            float dx=x-ox,dy=y-oy;
            float shape=Mathf.Abs(dx)*1.3f+Mathf.Abs(dy)*.65f;
            spark=Mathf.Max(spark,Mathf.Clamp01((.16f-i*.01f-shape)*27));
            glow=Mathf.Max(glow,Mathf.Exp(-shape*11)*.55f);
        }
        float tail=Stroke(y-x*(pattern%2==0?.6f:-.6f),.018f)*Mathf.Clamp01(.6f-Mathf.Abs(x));
        return Paint(Color.Lerp(ink,light,spark*.82f),Mathf.Max(spark,Mathf.Max(glow,tail)));
    }

    static Color Coin(float x,float y,float radius,Color ink,Color light){
        float edge=Stroke(radius-.76f,.055f),inner=Stroke(radius-.58f,.03f);
        float face=Mathf.Clamp01((.82f-radius)*31);
        float diamond=Stroke(Mathf.Abs(x)+Mathf.Abs(y)-.35f,.048f);
        Color shadow=ink*(.44f+.21f*(y-x+1));
        return Paint(Color.Lerp(shadow,light,Mathf.Max(diamond,Mathf.Max(edge*.79f,inner*.69f))),face);
    }

    static Color Frame(float x,float y,Color ink,Color light){
        float count=Mathf.Max(Mathf.Abs(x),Mathf.Abs(y));
        float rim=Stroke(count-.68f,.095f),glint=Stroke(count-.59f,.022f);
        float studs=Stroke(Mathf.Abs(Mathf.Abs(x)-.68f)+Mathf.Abs(Mathf.Abs(y)-.68f),.09f);
        return Paint(Color.Lerp(ink,light,Mathf.Max(glint,studs)),Mathf.Max(rim,studs));
    }
}
}
