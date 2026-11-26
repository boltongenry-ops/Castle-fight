using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    const float AbyssCastCue=1.35f,AbyssWaterCue=2.35f,AbyssCloseupEnd=6.25f,AbyssFloodEnd=10.05f;
    static readonly Vector2 AbyssPortalPoint=HumanMatch.CastlePosition(1)+new Vector2(-8,6);
    sealed class AbyssRitualActor {
        public PcModel view;
        public Vector2 point;
        public float crossedAt=-1;
        public Vector3 startScale,endScale;
    }
    sealed class AbyssPortalView {
        public GameObject root;
        public Mesh aperture,jet;
        public Material apertureMaterial,jetMaterial;
        public LineRenderer[] arcs;
        public Vector3[] jetVertices;
        public MeshRenderer jetRenderer;
        public int changed;
        public bool cast;
        public readonly List<AbyssRitualActor> witnesses=new List<AbyssRitualActor>(4);
    }

    void CreateAbyssPortal(Shader shader){
        var portal=abyss.portal;
        portal.root=new GameObject("Разлом Бездны");
        portal.root.transform.SetParent(abyss.root.transform,false);
        portal.apertureMaterial=new Material(shader){renderQueue=3020};
        portal.jetMaterial=new Material(shader){mainTexture=abyss.foamTexture,renderQueue=3000};
        var opening=new GameObject("Пасть портала");
        opening.transform.SetParent(portal.root.transform,false);
        opening.transform.position=World(AbyssPortalPoint)+Vector3.up*4.65f;
        var mesh=new Mesh{name="Portal aperture"};
        const int segments=40;
        var vertices=new Vector3[1+segments*2];
        var colors=new Color[vertices.Length];
        var triangles=new int[segments*9];
        colors[0]=new Color(.008f,.012f,.044f,.99f);
        for(int i=0;i<segments;i++){
            float angle=i*Mathf.PI*2/segments;
            float spines=1+.05f*Mathf.Sin(7*angle)+.04f*Mathf.Sin(11*angle);
            var radial=new Vector3(0,Mathf.Cos(angle)*5.9f,Mathf.Sin(angle)*4.25f)*spines;
            vertices[1+i]=radial*.72f;
            vertices[1+i+segments]=radial;
            colors[1+i]=new Color(.022f,.19f,.24f,.94f);
            colors[1+i+segments]=new Color(.26f,.56f,.7f,.16f);
            int a=1+i,b=1+(i+1)%segments,c=a+segments,d=b+segments,T=i*9;
            triangles[T]=0;triangles[T+1]=a;triangles[T+2]=b;
            triangles[T+3]=a;triangles[T+4]=c;triangles[T+5]=b;
            triangles[T+6]=b;triangles[T+7]=c;triangles[T+8]=d;
        }
        mesh.vertices=vertices;mesh.colors=colors;mesh.uv=new Vector2[vertices.Length];mesh.triangles=triangles;
        portal.aperture=mesh;
        opening.AddComponent<MeshFilter>().sharedMesh=mesh;
        opening.AddComponent<MeshRenderer>().sharedMaterial=portal.apertureMaterial;
        portal.arcs=new LineRenderer[3];
        for(int i=0;i<portal.arcs.Length;i++){
            var arc=CreateAbyssLine("Кромка разлома "+i,i==0?.25f:.1f);
            arc.transform.SetParent(portal.root.transform,false);
            arc.sharedMaterial=portal.apertureMaterial;
            arc.positionCount=33;
            portal.arcs[i]=arc;
        }
        var ribbon=new GameObject("Поток из разлома");
        ribbon.transform.SetParent(portal.root.transform,false);
        portal.jetRenderer=ribbon.AddComponent<MeshRenderer>();
        portal.jetRenderer.sharedMaterial=portal.jetMaterial;
        portal.jet=new Mesh{name="Abyss outflow"};
        portal.jet.MarkDynamic();
        const int rows=23;
        portal.jetVertices=new Vector3[rows*5];
        var uv=new Vector2[portal.jetVertices.Length];
        var tint=new Color[portal.jetVertices.Length];
        var faces=new int[(rows-1)*4*6];
        for(int i=0;i<rows;i++)for(int j=0;j<5;j++){
            int at=i*5+j;
            uv[at]=new Vector2(i*.21f,j*.25f);
            tint[at]=new Color(.83f,.95f,1,(j==0||j==4?.28f:.96f)*(1-.58f*Mathf.Pow(i/(rows-1f),3)));
            if(i>=rows-1||j>=4)continue;
            int f=(i*4+j)*6;
            faces[f]=at;faces[f+1]=at+5;faces[f+2]=at+1;
            faces[f+3]=at+1;faces[f+4]=at+5;faces[f+5]=at+6;
        }
        portal.jet.vertices=portal.jetVertices;portal.jet.uv=uv;portal.jet.colors=tint;portal.jet.triangles=faces;
        ribbon.AddComponent<MeshFilter>().sharedMesh=portal.jet;
        portal.root.SetActive(false);
    }

    void UpdateAbyssPortal(float t,float frontX){
        var portal=abyss.portal;
        float opened=Mathf.SmoothStep(0,1,Mathf.InverseLerp(AbyssCastCue,2.25f,t));
        float closed=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,9.4f,t));
        float size=opened*(1-closed);
        portal.root.SetActive(size>.01f);
        if(!portal.root.activeSelf)return;
        portal.root.transform.GetChild(0).localScale=Vector3.one*Mathf.Max(.01f,size);
        var origin=World(AbyssPortalPoint);
        for(int k=0;k<portal.arcs.Length;k++){
            var arc=portal.arcs[k];
            arc.startColor=new Color(.19f,.73f,.91f,size*.69f);
            arc.endColor=new Color(k==0?.71f:.45f,k==0?.96f:.48f,1,size);
            for(int i=0;i<arc.positionCount;i++){
                float angle=i/(arc.positionCount-1f)*(k==0?2*Mathf.PI:Mathf.PI*1.12f)+k*2.16f-t*(k+1)*.19f;
                float pulse=1+.045f*Mathf.Sin(angle*9+t*6)+k*.035f;
                arc.SetPosition(i,origin+new Vector3(-.22f-k*.08f,4.65f+Mathf.Cos(angle)*5.7f*size*pulse,
                    Mathf.Sin(angle)*4.2f*size*pulse));
            }
        }
        float extent=Mathf.Clamp(AbyssPortalPoint.x-frontX,0,65);
        portal.jetRenderer.enabled=t>=AbyssWaterCue&&extent>.2f;
        if(!portal.jetRenderer.enabled)return;
        for(int i=0;i<23;i++){
            float f=i/22f;
            float halfWidth=Mathf.Lerp(1.8f,5.2f+extent*.12f,f);
            float x=AbyssPortalPoint.x-extent*f;
            float center=AbyssPortalPoint.y+Mathf.Sin(f*2)*1.4f;
            for(int j=0;j<5;j++){
                float across=(j-2)*.5f;
                var point=new Vector2(x,center+halfWidth*across);
                float hump=(2.2f*Mathf.Pow(1-f,5)+1.25f*Mathf.Pow(f,6))*(1-.50f*Mathf.Abs(across));
                float wave=.19f*Mathf.Sin(18*f-t*9+across);
                portal.jetVertices[i*5+j]=AbyssSurfacePoint(point,.42f+hump+wave);
            }
        }
        portal.jet.vertices=portal.jetVertices;
    }

    void CrossAbyssRitualActors(float t,float edge){
        foreach(var actor in abyss.portal.witnesses){
            if(actor.crossedAt<0){
                if(t<AbyssWaterCue||AbyssShoreX(edge,actor.point.y)>actor.point.x)continue;
                actor.crossedAt=t;
                actor.startScale=actor.view.transform.localScale;
                DressAbyssActor(actor.view,actor.view==abyss.queen?1.24f:1.32f);
                actor.endScale=actor.view.transform.localScale;
                actor.view.transform.localScale=actor.startScale;
                actor.view.Play("Spell",1.35f,2);
                abyss.portal.changed++;
            }
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(actor.crossedAt,actor.crossedAt+.56f,t));
            actor.view.transform.localScale=Vector3.Lerp(actor.startScale,actor.endScale,blend);
        }
    }

    void DisposeAbyssPortal(){
        Destroy(abyss.portal.aperture);
        Destroy(abyss.portal.jet);
        Destroy(abyss.portal.apertureMaterial);
        Destroy(abyss.portal.jetMaterial);
    }
}
}
