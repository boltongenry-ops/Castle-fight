using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
// One vertex-coloured mesh for visible swamp spells in a match.
public sealed class PepeSkillFx : MonoBehaviour {
    readonly List<Vector3> points = new List<Vector3>(2048);
    readonly List<Color32> colors = new List<Color32>(2048);
    readonly List<int> triangles = new List<int>(3072);
    Mesh mesh;
    Material material;
    MeshRenderer surface;
    Vector3 origin;
    float radius, age, opacity, phase;
    static readonly Color Leaf = new Color(.25f,.74f,.24f), Mint = new Color(.67f,1f,.66f),
        Water = new Color(.16f,.82f,.9f), Gold = new Color(1f,.78f,.26f), Bronze = new Color(.65f,.32f,.10f),
        Pink = new Color(1f,.53f,.81f), Pearl = new Color(.94f,1f,.89f);

    void Awake(){
        var shader=PepeContent.Load<Shader>("Pepe/SwampMagic");
        if(!shader)return;
        mesh=new Mesh {name="Swamp spell geometry",indexFormat=IndexFormat.UInt32};
        mesh.MarkDynamic(); material=new Material(shader);
        var child=new GameObject("Swamp spells");child.transform.SetParent(transform,false);
        child.AddComponent<MeshFilter>().sharedMesh=mesh;
        surface=child.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
        surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
    }
    void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    public void Begin(){points.Clear();colors.Clear();triangles.Clear();}
    public void Finish(){
        if(!mesh)return;
        surface.enabled=points.Count>0;
        if(!surface.enabled)return;
        mesh.Clear();mesh.SetVertices(points);mesh.SetColors(colors);mesh.SetTriangles(triangles,0,true);
    }
    int Vertex(Vector3 local,Color color,float alpha=1){
        int index=points.Count;points.Add(origin+local*radius);color.a=opacity*alpha;colors.Add(color);return index;
    }
    void Tri(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
    static Vector3 Rad(float a)=>new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
    void Petal(Vector3 basePoint,Vector3 tip,Vector3 width,Color baseColor,Color edge){
        var mid=Vector3.Lerp(basePoint,tip,.52f);int a=Vertex(basePoint,baseColor),b=Vertex(mid+width,baseColor),
            c=Vertex(tip,edge),d=Vertex(mid-width,baseColor),e=Vertex(mid+Vector3.up*width.magnitude*.32f,edge);
        Tri(a,b,e);Tri(b,c,e);Tri(c,d,e);Tri(d,a,e);
    }
    void Wave(float size,float y,float width,Color tint,float start=0,float span=6.283185f){
        Vector3 center=Vector3.up*y;
        int previous=-1;
        for(int i=0;i<=28;i++){
            var arm=Rad(start+span*i/28);int inner=Vertex(center+arm*(size-width),tint,0);
            int rim=Vertex(center+arm*size,tint,.94f);Vertex(center+arm*(size+width*.45f),tint,0);
            if(previous>=0){Tri(previous,inner,rim);Tri(previous,rim,previous+1);Tri(previous+1,rim,rim+1);Tri(previous+1,rim+1,previous+2);}
            previous=inner;
        }
    }
    void Lily(float direction,float size,Color face){
        Vector3 center=Rad(direction)*.40f+Vector3.up*.12f;
        int hub=Vertex(center,Mint),last=-1;
        for(int i=0;i<=15;i++){
            var axis=Rad(direction+.30f+i*(2*Mathf.PI-.60f)/15);
            int next=Vertex(center+axis*size,face);if(last>=0)Tri(hub,last,next);last=next;
        }
        var vein=Rad(direction+Mathf.PI);Petal(center,center+vein*size,Rad(direction+Mathf.PI*.5f)*.035f,Mint,face);
    }
    void Reeds(int count,bool barbed){
        for(int i=0;i<count;i++){
            var dir=Rad(phase+i*2*Mathf.PI/count);var side=Vector3.Cross(dir,Vector3.up);
            var foot=dir*(.15f+.25f*age);var tip=foot+dir*(.46f+.29f*age)+Vector3.up*(1.05f-age*.22f);
            Petal(foot,tip,side*.055f,Bronze,Mint);
            Petal(tip-dir*.19f-Vector3.up*.24f,tip+Vector3.up*.14f,side*(barbed?.16f:.09f),barbed?Gold:Leaf,Pearl);
            if(barbed)Petal(tip,tip-side*.18f-Vector3.up*.21f,dir*.06f,Gold,Bronze);
        }
    }
    void Shell(int ribs,Color edge){
        float swell=.60f+.20f*Mathf.Sin(age*Mathf.PI);
        for(int i=0;i<ribs;i++){
            float a=i*2*Mathf.PI/ribs;var radial=Rad(a);var cross=Rad(a+Mathf.PI/2);
            Petal(radial*swell,Vector3.up*1.20f,cross*.25f,Leaf,edge);
        }
        Wave(swell,.07f,.10f,edge);
    }
    void Wings(){
        var center=Vector3.up*(.55f+.32f*age);
        for(int i=0;i<4;i++){
            float a=phase+(i/2)*Mathf.PI+(i%2==0?.54f:-.50f);
            var dir=Rad(a);Petal(center,center+dir*(1.08f-age*.2f)+Vector3.up*(.25f*Mathf.Sin(age*16)),Rad(a+1.57f)*.20f,Water,Pearl);
        }
    }
    void Bloom(){
        var center=Vector3.up*(.22f+age*.7f);
        for(int i=0;i<7;i++){float a=i*.8976f;Petal(center,center+Rad(a)*(.42f+.40f*age)+Vector3.up*(.54f-.30f*age),Rad(a+1.57f)*.18f,Pink,Pearl);}
    }
    public void Shield(Vector3 feet,float height,float time,int id) {
        if(!mesh)return;
        origin=transform.InverseTransformPoint(feet);radius=1;
        opacity=.85f;phase=time*.9f+id;
        float width=height*.46f;
        const int slices=18;
        for (int i=0;i<slices;i++) {
            float a=i*2*Mathf.PI/slices;
            var side=Rad(a);var next=Rad(a+2*Mathf.PI/slices);
            int left=Vertex(side*width*.68f+Vector3.up*height*.13f,Water,.12f);
            int right=Vertex(next*width*.68f+Vector3.up*height*.13f,Water,.12f);
            int upper=Vertex(side*width+Vector3.up*height*.62f,Mint,.22f);
            int end=Vertex(next*width+Vector3.up*height*.62f,Mint,.22f);
            int cap=Vertex(Vector3.up*height*1.09f,Water,.05f);
            Tri(left,right,upper);Tri(upper,right,end);Tri(upper,end,cap);
        }
        Wave(width*.78f,height*.20f,width*.07f,Mint);
        for(int i=0;i<3;i++){
            var dir=Rad(phase+i*2*Mathf.PI/3);var tangent=Vector3.Cross(dir,Vector3.up);
            var crest=dir*width+Vector3.up*height*.67f;
            Petal(crest+Vector3.up*height*.16f,crest-Vector3.up*height*.18f,tangent*width*.24f,Water,Mint);
        }
    }
    public bool Paint(string id,Vector3 point,float size,float progress,int seed){
        if(!mesh)return false;
        origin=transform.InverseTransformPoint(point+Vector3.up*.16f);radius=Mathf.Max(.65f,size);
        age=Mathf.Clamp01(progress);opacity=Mathf.Clamp01((1-age)*3);phase=(seed%31)*.2f;
        switch(id){
            case "pepe:PMrk": case "pepe:PMr2":case "pepe:PTmp":
                int leaves=id=="pepe:PMr2"?5:3;
                for(int i=0;i<leaves;i++)Lily(phase+i*2*Mathf.PI/leaves,.22f+.13f*age,Leaf);
                Wave(.35f+.6f*age,.03f,.10f,Water);break;
            case "pepe:PGSh":case "pepe:PShd":Shell(7,Mint);break;
            case "pepe:PBr1": case "pepe:PBr2":Shell(id=="pepe:PBr1"?4:6,Gold);Reeds(3,false);break;
            case "pepe:PAr1":case "pepe:PAr2":Reeds(id=="pepe:PAr1"?3:5,id=="pepe:PAr2");break;
            case "pepe:PSg1":case "pepe:PSg2":
                Wave(.25f+.65f*age,.04f,.11f,Water);Reeds(id=="pepe:PSg1"?4:6,true);break;
            case "pepe:PEva":Wings();break;
            case "pepe:PDiv":Wings();Wave(.2f+.8f*age,.07f,.12f,Water);break;
            case "pepe:PHea":Bloom();break;
            case "pepe:PHe2":Bloom();Wave(.28f+.6f*age,.16f,.06f,Mint);break;
            case "pepe:reed-tower":Reeds(5,true);break;
            case "steppe:charge":case "steppe:break":case "steppe:harpoon":case "steppe:needles":
                for(int i=0;i<5;i++){var dir=Rad(phase+i*1.2566f);Petal(dir*(.2f+age*.18f),dir*(.5f+.48f*age)+Vector3.up*.52f,Rad(phase+i*1.2566f+1.57f)*.07f,Bronze,Gold);}break;
            case "steppe:stomp":case "steppe:sand":
                for(int i=0;i<4;i++)Wave(.38f+.48f*Mathf.Repeat(age*2+i*.25f,1),.06f+.10f*i,.055f,i%2==0?Gold:Bronze,phase+i*1.57f+age*4,2.3f);break;
            case "jade:panda_brew":case "jade:panda_keg":
                Wave(.22f+age*.72f,.04f,.09f,Bronze);
                for(int i=0;i<8;i++){var dir=Rad(phase+i*.785f);var drop=dir*(.2f+.65f*age)+Vector3.up*(.12f+.7f*Mathf.Sin(age*Mathf.PI));Petal(drop,drop+Vector3.up*.16f,Rad(phase+i*.785f+1.57f)*.055f,Gold,Pearl);}break;
            case "jade:panda_gust":
                for(int i=0;i<3;i++)Wave(.3f+.18f*i+age*.22f,.18f*i,.075f,Mint,phase+age*5+i*2.1f,3.3f);break;
            case "jade:panda_stomp":case "jade:panda_tower":case "jade:combo":
                Wave(.18f+.78f*age,.08f,.09f,Gold);
                for(int i=0;i<4;i++){var dir=Rad(phase+i*1.57f);Petal(dir*.15f,dir*(.52f+age*.42f)+Vector3.up*.37f,Rad(phase+(i+1)*1.57f)*.09f,Mint,Pearl);}break;
            case "pepe:PLea":
                for(int i=0;i<6;i++){float a=phase+i*1.0472f;var d=Rad(a);var tip=d*(.4f+age*.6f)+Vector3.up*(.15f+.75f*Mathf.Sin(age*Mathf.PI));Petal(tip-d*.17f,tip+Vector3.up*.18f,Rad(a+1.57f)*.08f,Water,Pearl);}break;
            case "pepe:PQua":
                for(int i=0;i<3;i++)Wave(.12f+Mathf.Repeat(age+.33f*i,1)*.88f,.06f+i*.035f,.07f,i==1?Gold:Water);break;
            case "pepe:PHrn":case "pepe:PHr2":
                int echoes=id=="pepe:PHr2"?4:2;
                for(int i=0;i<echoes;i++)Wave(.26f+.63f*Mathf.Repeat(age+(float)i/echoes,1),.12f*(i+1),.08f,Gold);
                Reeds(2,false);break;
            case "pepe:PLil":
                for(int i=0;i<6;i++)Lily(phase+i*Mathf.PI/3,.23f+.18f*age,Leaf);Bloom();break;
            case "pepe:PBll":
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Petal(Rad(a)*.58f,Rad(a)*.28f+Vector3.up*.88f,Rad(a+1.57f)*.23f,Bronze,Gold);}
                Wave(.58f,.03f,.10f,Gold);Wave(.30f+.70f*age,.04f,.09f,Mint);break;
            default:return false;
        }
        return true;
    }
}
public sealed partial class PcGame {
    void RenderPepeSkills(){
        visuals.TryGetComponent<PepeSkillFx>(out var batch);
        if(batch)batch.Begin();
        foreach(var p in match.Pulses){
            if(p.fx==null||!(p.fx.StartsWith("pepe:",System.StringComparison.Ordinal)||p.fx.StartsWith("jade:",System.StringComparison.Ordinal)||p.fx.StartsWith("steppe:",System.StringComparison.Ordinal))||!ShowPulse036(p))continue;
            if(!batch)batch=visuals.AddComponent<PepeSkillFx>();
            var center=World(p.position);
            if(p.visualUnitId>0&&units.TryGetValue(p.visualUnitId,out var unit)&&unit)
                center=unit.transform.position;
            if(batch.Paint(p.fx,center,p.radius,p.age/p.duration,p.id))Art036.rendered.Add(p.id);
        }
        foreach(var fighter in match.Units){
            if(fighter.hp<=0 || fighter.effects.Count==0 || match.PepeShield(fighter)==null ||
                !units.TryGetValue(fighter.id,out var view) || !ShowUnitArt036(fighter,view))continue;
            if(!batch)batch=visuals.AddComponent<PepeSkillFx>();
            float height=view.godotHeight>0?view.godotHeight:Mathf.Clamp(1.8f+Mathf.Sqrt(fighter.maxHp)*.025f,2.1f,3.7f);
            batch.Shield(view.transform.position,height,Time.time,fighter.id);
        }
        if(batch)batch.Finish();
    }
}
}
