using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const int AbyssColumns=96,AbyssRows=56;
    const float AbyssIntroDuration=12.15f;
    static readonly Color AbyssAccent=new Color(.44f,.94f,.81f);
    static readonly Color AbyssDeepTint=new Color(.40f,.80f,1f);
    static readonly Rect AbyssHud=new Rect(279,33,262,85);
    static readonly string[] AbyssIntroGuards={"n00X","n00J","n01P"};
    static readonly Vector2 AbyssQueenPosition=HumanMatch.CastlePosition(1)+new Vector2(-14,2);
    bool abyssIntro,abyssVersus;
    float abyssIntroTime;
    Texture2D abyssPainting;
    GUIStyle abyssHudTitle,abyssHudCopy;
    AbyssPresentation abyss;

    sealed class AbyssPresentation {
        public GameObject root,actors;
        public PcModel queen;
        public Mesh surface,crest;
        public Material water,foam,crestMaterial;
        public Texture2D ripples,foamTexture,hudFrame;
        public int[] triangles;
        public Vector2[] uv;
        public bool offsetsInShader;
        public float nextUvUpdate;
        public Vector3[] originalVertices,shoreVertices;
        public int columns=-1;
        public LineRenderer front,frontWash,raid,raidFoam;
        public AbyssPortalView portal=new AbyssPortalView();
        public AudioSource audio;
        public AudioClip surge;
    }

    static JObject AbyssDefinition()=>new JObject{
        ["id"]=HumanMatch.AbyssTrial,["title"]="Дыхание Бездны",["opponent"]="Naga Faction",
        ["summary"]="Затопленная карта, глубинные наги и вылазки у замка.",
        ["description"]="Наги отстраивают базу с нуля. Вода замедляет наземных бойцов на 25% и даёт 10% промаха. Первая вылазка приходит на 6:30. После потери половины HP замка в вылазках появляется гидра.",
        ["start_gold"]=620,["opponent_castle_hp"]=HumanMatch.AbyssCastleHealth,
        ["difficulty"]=2,["reward"]=0,["xp"]=0
    };

    bool ActiveAbyssTrial=>(string)activeChallenge?["id"]==HumanMatch.AbyssTrial;

    void DrawAbyssPainting(Rect area){
        if(!abyssPainting)abyssPainting=LoadProtocolTexture("AbyssalTide","jpg",TextureFormat.RGB24,false)??MobileVersusPicture(2);
        Fill(area,new Color(.014f,.071f,.085f));
        if(abyssPainting){
            float height=Mathf.Min(1,area.height*abyssPainting.width/(area.width*abyssPainting.height));
            float width=Mathf.Min(1,area.width*abyssPainting.height/(area.height*abyssPainting.width));
            GUI.DrawTextureWithTexCoords(area,abyssPainting,new Rect((1-width)*.78f,(1-height)*.52f,width,height),false);
        }
        WinterGradient(new Rect(area.x,area.y,area.width*.62f,area.height),true,new Color(.012f,.048f,.060f,.92f));
    }

    void BeginAbyssIntro(){
        abyssIntro=true;
        abyssIntroTime=0;
        var shader=PrepareAbyssWaterPresentation();
        abyss.actors=new GameObject("Двор Королевы глубин");
        abyss.actors.transform.SetParent(abyss.root.transform,false);
        abyss.queen=CreateAbyssActor(HumanMatch.AbyssQueenCode,AbyssQueenPosition,1.7f);
        for(int i=0;i<AbyssIntroGuards.Length;i++)
            CreateAbyssActor(AbyssIntroGuards[i],AbyssQueenPosition+new Vector2(-7-7*i,-7+i*6),1);
        CreateAbyssPortal(shader);
        abyss.audio=abyss.root.AddComponent<AudioSource>();
        abyss.audio.playOnAwake=false;
        abyss.surge=MakeAbyssSurge();
        UpdateAbyssIntro(false);
    }

    Shader PrepareAbyssWaterPresentation(){
        abyss=new AbyssPresentation{root=new GameObject("Дыхание Бездны")};
        abyss.root.transform.SetParent(visuals.transform,false);
        var shader=Shader.Find("Sprites/Default")??shellMaterial.shader;
        abyss.ripples=MakeAbyssRipples();
        var waterShader=Shader.Find("CastleFight/ReforgedEffect");
        abyss.water=new Material(waterShader?waterShader:shader){mainTexture=abyss.ripples,renderQueue=2980};
        abyss.offsetsInShader=waterShader;
        if(waterShader)abyss.water.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        abyss.foamTexture=MakeAbyssFoam();
        abyss.foam=new Material(shader){mainTexture=abyss.foamTexture,renderQueue=2990};
        abyss.crestMaterial=new Material(shader){renderQueue=3005};
        abyss.crest=CreateAbyssCrest();
        abyss.hudFrame=LoadProtocolTexture("AbyssMeter","png",TextureFormat.RGBA32,false);
        abyss.front=CreateAbyssLine("Гребень прилива",1.42f);
        abyss.frontWash=CreateAbyssLine("След прилива",2.9f);
        abyss.raid=CreateAbyssLine("Водоворот подкрепления",1.1f);
        abyss.raidFoam=CreateAbyssLine("Пена водоворота",.36f);
        abyss.raid.enabled=false;
        abyss.raidFoam.enabled=false;
        CreateAbyssWater();
        return shader;
    }

    PcModel CreateAbyssActor(string raw,Vector2 point,float size){
        int kind=Array.FindIndex(match.Lines,line=>line.unit==raw);
        if(kind<0||!(RaceModel(1,kind,false) is GameObject prefab)||!prefab)return null;
        var actor=Instantiate(prefab,abyss.actors.transform).GetComponent<PcModel>();
        if(!actor)return null;
        actor.transform.position=World(point);
        actor.transform.localScale*=size;
        actor.Face(Vector2.left,true);
        actor.Play("Stand");
        abyss.portal.witnesses.Add(new AbyssRitualActor{view=actor,point=point});
        return actor;
    }

    static Vector2 AbyssWaterPoint(float column,int row)=>new Vector2(
        Mathf.Lerp(HumanMatch.AbyssWorldEast,-HumanMatch.AbyssWorldEast,column/(float)AbyssColumns),
        Mathf.Lerp(-HumanMatch.AbyssWorldNorth,HumanMatch.AbyssWorldNorth,row/(float)AbyssRows));

    Vector3 AbyssSurfacePoint(Vector2 point,float lift=.31f)=>World(point)+Vector3.up*lift;

    float AbyssShoreX(float edge,float y){
        float part=edge-Mathf.Floor(edge);
        float blend=Mathf.Clamp01(part*3)*(1-Mathf.Clamp01((part-.66f)*3));
        float row=Mathf.InverseLerp(-HumanMatch.AbyssWorldNorth,HumanMatch.AbyssWorldNorth,y)*AbyssRows;
        return Mathf.Lerp(HumanMatch.AbyssWorldEast,-HumanMatch.AbyssWorldEast,edge/AbyssColumns)+
            1.15f*blend*Mathf.Sin(row*.26f+abyssIntroTime*2);
    }

    void CreateAbyssWater(){
        var surface=new GameObject("Поверхность затопления");
        surface.transform.SetParent(abyss.root.transform,false);
        var vertices=new Vector3[(AbyssColumns+1)*(AbyssRows+1)];
        var uv=new Vector2[vertices.Length];
        var colors=new Color32[vertices.Length];
        abyss.triangles=new int[AbyssColumns*AbyssRows*6];
        for(int col=0;col<=AbyssColumns;col++)
            for(int row=0;row<=AbyssRows;row++){
                int i=col*(AbyssRows+1)+row;
                var point=AbyssWaterPoint(col,row);
                vertices[i]=AbyssSurfacePoint(point);
                uv[i]=point*.027f;
                colors[i]=new Color32(255,255,255,255);
                if(col>=AbyssColumns||row>=AbyssRows)continue;
                int left=i+AbyssRows+1,t=(col*AbyssRows+row)*6;
                abyss.triangles[t]=i;
                abyss.triangles[t+1]=left;
                abyss.triangles[t+2]=i+1;
                abyss.triangles[t+3]=i+1;
                abyss.triangles[t+4]=left;
                abyss.triangles[t+5]=left+1;
            }
        abyss.surface=new Mesh{name="Abyss water"};
        abyss.surface.MarkDynamic();
        abyss.surface.vertices=vertices;
        abyss.originalVertices=vertices;
        abyss.shoreVertices=(Vector3[])vertices.Clone();
        abyss.surface.uv=uv;
        abyss.surface.colors32=colors;
        abyss.uv=uv;
        abyss.surface.RecalculateBounds();
        surface.AddComponent<MeshFilter>().sharedMesh=abyss.surface;
        surface.AddComponent<MeshRenderer>().sharedMaterial=abyss.water;
    }

    LineRenderer CreateAbyssLine(string name,float width){
        var obj=new GameObject(name);
        obj.transform.SetParent(abyss.root.transform,false);
        var line=obj.AddComponent<LineRenderer>();
        line.sharedMaterial=abyss.foam;
        line.useWorldSpace=true;
        line.startWidth=line.endWidth=width;
        line.textureMode=LineTextureMode.Tile;
        line.textureScale=new Vector2(.14f,1);
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var edge=new Color(.54f,1,.9f,.80f);
        line.startColor=line.endColor=edge;
        return line;
    }

    static Texture2D MakeAbyssRipples(){
        const int size=128;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){
            name="Abyss ripples",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear
        };
        var colors=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float u=x/(float)size,v=y/(float)size;
            float swell=AbyssNoise(u,v,4,5,7);
            float eddies=AbyssNoise(u,v,9,13,23);
            float flecks=AbyssNoise(u,v,31,41,43);
            float grain=.60f*swell+.3f*eddies+.10f*flecks;
            float glint=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.92f,eddies*.63f+flecks*.37f));
            glint*=glint*(.2f+.8f*swell);
            colors[y*size+x]=new Color(.028f+grain*.065f+glint*.24f,
                .14f+grain*.16f+glint*.33f,.23f+grain*.20f+glint*.38f,.59f+grain*.11f);
        }
        texture.SetPixels32(colors);
        texture.Apply(false,true);
        return texture;
    }

    static float AbyssNoise(float u,float v,int nx,int ny,int seed){
        float x=u*nx,y=v*ny;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float a=x-ix,b=y-iy;
        a=a*a*(3-2*a);b=b*b*(3-2*b);
        return Mathf.Lerp(Mathf.Lerp(AbyssHash(ix,iy,nx,ny,seed),AbyssHash(ix+1,iy,nx,ny,seed),a),
            Mathf.Lerp(AbyssHash(ix,iy+1,nx,ny,seed),AbyssHash(ix+1,iy+1,nx,ny,seed),a),b);
    }

    static float AbyssHash(int x,int y,int nx,int ny,int seed){
        uint bits=unchecked((uint)(((x%nx+nx)%nx)*374761393+((y%ny+ny)%ny)*668265263+seed*131));
        bits=unchecked((bits^(bits>>13))*1274126177u);
        return ((bits^(bits>>16))&0xffff)/65535f;
    }

    static Texture2D MakeAbyssFoam(){
        const int width=256,height=32;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){
            name="Abyss breaking foam",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear
        };
        var colors=new Color32[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            float u=x/(float)width,v=y/(height-1f);
            float shift=(AbyssNoise(u,0,13,1,59)-.5f)*.50f;
            float strand=1-Mathf.Clamp01(Mathf.Abs(v-.5f+shift)*2.7f);
            float froth=AbyssNoise(u,v,43,5,79);
            float grain=AbyssNoise(u,v,109,13,101);
            float pooling=AbyssNoise(u,0,17,1,107);
            float edge=Mathf.SmoothStep(0,1,Mathf.Min(v,1-v)*8);
            float alpha=edge*strand*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,.77f,froth*.55f+grain*.30f+pooling*.15f));
            colors[y*width+x]=new Color(.74f+grain*.23f,.89f+grain*.10f,1,alpha);
        }
        texture.SetPixels32(colors);
        texture.Apply(false,true);
        return texture;
    }

    void SetAbyssShore(float progress){
        float edge=Mathf.Clamp01(progress)*AbyssColumns;
        int columns=Mathf.CeilToInt(edge);
        int stride=AbyssRows+1;
        if(abyss.columns!=columns){
            if(abyss.columns>=0)Array.Copy(abyss.originalVertices,abyss.columns*stride,abyss.shoreVertices,abyss.columns*stride,stride);
            abyss.columns=columns;
            abyss.surface.SetTriangles(abyss.triangles,0,columns*AbyssRows*6,0,false);
        }
        abyss.front.enabled=edge>0&&edge<AbyssColumns;
        abyss.frontWash.enabled=abyss.front.enabled;
        if(!abyss.front.enabled){
            if(columns==AbyssColumns)Array.Copy(abyss.originalVertices,columns*stride,abyss.shoreVertices,columns*stride,stride);
            abyss.surface.vertices=abyss.shoreVertices;
            return;
        }
        abyss.front.positionCount=AbyssRows+1;
        abyss.frontWash.positionCount=AbyssRows+1;
        abyss.front.startColor=abyss.front.endColor=new Color(.89f,.97f,1,.88f);
        abyss.frontWash.startColor=abyss.frontWash.endColor=new Color(.55f,.82f,.86f,.48f);
        for(int i=0;i<=AbyssRows;i++){
            var point=AbyssWaterPoint(edge,i);
            point.x=AbyssShoreX(edge,point.y);
            var water=AbyssSurfacePoint(point);
            abyss.shoreVertices[columns*stride+i]=water;
            abyss.front.SetPosition(i,water+Vector3.up*.12f);
            point.x+=1.6f+.65f*Mathf.Sin(i*.47f+abyssIntroTime*3.2f);
            abyss.frontWash.SetPosition(i,AbyssSurfacePoint(point,.39f));
        }
        abyss.surface.vertices=abyss.shoreVertices;
    }

    void UpdateAbyssIntro(bool advance=true){
        if(advance)abyssIntroTime+=Time.unscaledDeltaTime;
        controls.InputBlocked=true;
        controls.EdgePan=false;
        controls.GameplayViewport(false);
        float t=abyssIntroTime;
        var queen=World(AbyssQueenPosition)+Vector3.up*3;
        var keep=World(HumanMatch.CastlePosition(1))+Vector3.up*4;
        var portal=World(AbyssPortalPoint)+Vector3.up*4;
        float front=t<AbyssCloseupEnd?Mathf.Lerp(AbyssPortalPoint.x,AbyssPortalPoint.x-32,
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(AbyssWaterCue,AbyssCloseupEnd,t))):
            Mathf.Lerp(AbyssPortalPoint.x-32,-HumanMatch.AbyssWorldEast,
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(AbyssCloseupEnd,AbyssFloodEnd,t)));
        float shore=t<AbyssWaterCue?0:Mathf.InverseLerp(HumanMatch.AbyssWorldEast,-HumanMatch.AbyssWorldEast,front);
        SetAbyssShore(shore);
        AnimateAbyssWater();
        UpdateAbyssPortal(t,front);
        CrossAbyssRitualActors(t,shore*AbyssColumns);
        if(t>=AbyssCastCue&&!abyss.portal.cast){
            abyss.portal.cast=true;
            if(abyss.queen)abyss.queen.Play("Spell",1,1);
            abyss.audio.PlayOneShot(abyss.surge,.68f);
        }
        if(t<2.3f)FrameWinterCamera(Vector3.Lerp(keep,Vector3.Lerp(queen,portal,.60f),t/2.3f),39-13*(t/2.3f),56-9*t,28);
        else if(t<AbyssCloseupEnd){
            float follow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.3f,AbyssCloseupEnd,t));
            var target=World(new Vector2(front,AbyssPortalPoint.y))+Vector3.up*2.6f;
            FrameWinterCamera(Vector3.Lerp(portal,target,.25f+.56f*follow),32+13*follow,69-40*follow,35+13*follow);
        }
        else{
            float travel=Mathf.SmoothStep(0,1,Mathf.InverseLerp(AbyssCloseupEnd,AbyssIntroDuration,t));
            var target=World(new Vector2(front,AbyssPortalPoint.y));
            FrameWinterCamera(Vector3.Lerp(target,World(match.Builders[0].position),travel*travel),45+39*Mathf.Sin(travel*Mathf.PI),25*(1-travel),50+13*Mathf.Sin(travel*Mathf.PI));
        }
        if(t>=AbyssIntroDuration||advance&&Input.GetKeyDown(KeyCode.Escape))EndAbyssIntro();
    }

    void EndAbyssIntro(){
        abyssIntro=false;
        SetAbyssShore(1);
        abyss.actors.SetActive(false);
        abyss.portal.root.SetActive(false);
        accumulator=0;
        controls.InputBlocked=false;
        controls.GameplayViewport(true);
        controls.FocusWorld(World(match.Builders[0].position),40);
        UpdateFog(true);
        ApplyFogViews();
    }

    static AudioClip MakeAbyssSurge(){
        const int rate=16000;
        var samples=new float[rate*5];
        uint random=13709;
        float low=0;
        for(int i=0;i<samples.Length;i++){
            float t=i/(float)rate;
            random=unchecked(1664525*random+1013904223);
            float noise=(random>>16)/32768f-1;
            low=Mathf.Lerp(low,noise,.026f);
            float swell=Mathf.Clamp01(t*2)*Mathf.Clamp01((5-t)*.54f);
            samples[i]=swell*(.38f*low+.12f*Mathf.Sin((44*t-1.5f*t*t)*2*Mathf.PI)+.033f*noise);
        }
        var clip=AudioClip.Create("Прилив Бездны",samples.Length,1,rate,false);
        clip.SetData(samples,0);
        return clip;
    }

    void AnimateAbyssWater(){
        float t=Time.unscaledTime;
        var offset=new Vector2((t*.032f)%1,(t*.014f)%1);
        if(abyss.offsetsInShader){abyss.water.mainTextureOffset=offset;return;}
        if(t<abyss.nextUvUpdate)return;
        abyss.nextUvUpdate=t+1f/12;
        for(int x=0;x<=AbyssColumns;x++)
            for(int y=0;y<=AbyssRows;y++)
                abyss.uv[x*(AbyssRows+1)+y]=AbyssWaterPoint(x,y)*.027f+offset;
        abyss.surface.uv=abyss.uv;
    }

    void RenderAbyssTide(){
        if(abyss==null||abyssIntro)return;
        AnimateAbyssWater();
        abyss.raid.enabled=match.AbyssRaidWarning;
        abyss.raidFoam.enabled=abyss.raid.enabled;
        if(!abyss.raid.enabled)return;
        float strength=1-match.AbyssRaidIn/HumanMatch.AbyssUndertowWarning;
        float radius=4.2f+2.9f*strength+Mathf.Sin(Time.unscaledTime*4)*.45f;
        var center=match.AbyssRaidPoint;
        const int nodes=49;
        abyss.raid.positionCount=nodes;
        abyss.raidFoam.positionCount=nodes;
        abyss.raid.startColor=new Color(.02f,.24f,.36f,.86f);
        abyss.raid.endColor=new Color(.11f,.61f,.69f,.54f);
        abyss.raidFoam.startColor=new Color(.35f,.95f,.84f,.30f);
        abyss.raidFoam.endColor=new Color(.88f,1,1,.95f);
        float phase=Time.unscaledTime*(3.1f+strength);
        for(int i=0;i<nodes;i++){
            float turn=i/(float)(nodes-1);
            float angle=turn*4.75f*Mathf.PI+phase;
            var offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*turn;
            abyss.raid.SetPosition(i,AbyssSurfacePoint(center+offset,.46f));
            abyss.raidFoam.SetPosition(i,AbyssSurfacePoint(center+offset*.84f,.54f));
        }
    }

    void StyleAbyssActor(PcModel view,HumanMatch.Unit unit){
        if(!match.AbyssDeepForm(unit))return;
        var raw=match.Lines[unit.kind].unit;
        DressAbyssActor(view,match.AbyssSeaHunter(unit)||raw=="n01P"?1.34f:raw=="n007"?1.30f:1.24f);
    }

    string AbyssSkillDescription(HumanMatch.Unit unit){
        switch(match.Lines[unit.kind].unit){
            case "n00S":return "Песня впадины\nДо четырёх целей. Немота и −25% к скорости атаки на 4 с.";
            case "n00H":return "Двойное жало\nДополнительный снаряд по соседнему врагу.";
            case "n01P":return "Три пасти\nДва дополнительных снаряда по соседним целям.";
            case "n007":return "Глубинная тварь\nПри обстреле постройки выпускает тварь с 320 HP.";
            default:return "Сброшенный панцирь\nУ ближних бойцов лопается на половине HP.";
        }
    }

    void DressAbyssActor(PcModel view,float scale){
        view.SetEffectTint(ApplyAbyssSkin(view)?new Color(.90f,.94f,1):AbyssDeepTint);
        view.transform.localScale*=scale;
        float height=Mathf.Max(1,view.ClassicHeight);
        var crest=new GameObject("Плавники глубин");
        crest.transform.SetParent(view.transform,false);
        float invScale=1/Mathf.Max(.01f,Mathf.Abs(view.transform.lossyScale.x));
        crest.transform.localPosition=new Vector3(0,height*.69f*invScale,0);
        crest.transform.localScale=Vector3.one*(height*.27f*invScale);
        crest.AddComponent<MeshFilter>().sharedMesh=abyss.crest;
        var renderer=crest.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=abyss.crestMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Mesh CreateAbyssCrest(){
        var mesh=new Mesh{name="Abyss fin crest"};
        mesh.vertices=new[]{
            new Vector3(-.2f,0,0),new Vector3(-.56f,.38f,-.05f),new Vector3(-1.04f,1.04f,-.25f),new Vector3(-.52f,.22f,-.37f),
            new Vector3(.2f,0,0),new Vector3(.56f,.38f,-.05f),new Vector3(1.04f,1.04f,-.25f),new Vector3(.52f,.22f,-.37f),
            new Vector3(-.21f,.10f,-.25f),new Vector3(0,.82f,-.36f),new Vector3(.21f,.10f,-.25f)
        };
        var dark=new Color(.015f,.24f,.38f,.87f);
        var light=new Color(.28f,.95f,.78f,.91f);
        mesh.colors=new[]{dark,light,light,dark,dark,light,light,dark,dark,light,dark};
        mesh.uv=new Vector2[11];
        mesh.triangles=new[]{0,1,2,0,2,3,4,6,5,4,7,6,8,9,10};
        return mesh;
    }

    void DrawAbyssIntro(){
        Fill(new Rect(0,0,1280,65),Color.black);
        Fill(new Rect(0,611,1280,109),Color.black);
        string text=abyssIntroTime<AbyssWaterCue?"Королева открывает разлом":abyssIntroTime<AbyssCloseupEnd?
            "Волна пробуждает глубинную форму":"Прилив накрывает поле боя";
        FitBossText(new Rect(49,628,910,52),text,new GUIStyle(heading){normal={textColor=AbyssAccent}},26);
        if(LobbyButton("abyss.skip",new Rect(1061,642,167,42),"Пропустить"))EndAbyssIntro();
    }

    void DrawAbyssHud(){
        if(abyss.hudFrame)GUI.DrawTexture(AbyssHud,abyss.hudFrame,ScaleMode.StretchToFill,true);
        else{Fill(AbyssHud,new Color(.014f,.071f,.085f,.96f));Border(AbyssHud,AbyssAccent);}
        if(abyssHudTitle==null){
            abyssHudTitle=new GUIStyle(label){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(),normal={textColor=AbyssAccent}};
            abyssHudCopy=new GUIStyle(small){fontSize=13,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
        }
        float x=AbyssHud.x+29,y=AbyssHud.y,w=AbyssHud.width-44;
        GUI.Label(new Rect(x,y+9,w,23),"Дыхание Бездны",abyssHudTitle);
        GUI.Label(new Rect(x,y+32,w,19),"−25% движения  ·  10% промаха",abyssHudCopy);
        string raid=match.AbyssRaidWarning?"Водоворот у замка!":(match.AbyssSecondPhase?"Гидра через ":"Вылазка через ")+
            TimeSpan.FromSeconds(Mathf.CeilToInt(match.AbyssRaidIn)).ToString(@"mm\:ss");
        GUI.Label(new Rect(x,y+50,w,22),raid,abyssHudCopy);
    }

    void ClearAbyss(){
        abyssIntro=false;
        if(abyss==null)return;
        abyss.root.SetActive(false);
        DisposeAbyssPortal();
        DisposeAbyssSkins();
        Destroy(abyss.root);
        Destroy(abyss.surface);
        Destroy(abyss.water);
        Destroy(abyss.foam);
        Destroy(abyss.crest);
        Destroy(abyss.crestMaterial);
        Destroy(abyss.hudFrame);
        Destroy(abyss.ripples);
        Destroy(abyss.foamTexture);
        Destroy(abyss.surge);
        abyss=null;
    }
}
}
