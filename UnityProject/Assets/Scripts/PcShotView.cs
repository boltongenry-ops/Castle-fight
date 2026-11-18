using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace CastleFight {
public sealed class PcShotView : MonoBehaviour {
    Transform body,flash;LineRenderer streak;MaterialPropertyBlock block;PcModel nativeModel;GameObject nativePrefab,nativeObject;
    HumanMatch.Shot observed;float observedAge,previousDuration,currentDuration,previousHeight,currentHeight,visualBlend=1;
    Vector2 previousEnd,currentEnd;bool hitObserved;
    public PcModel NativeModel=>nativeModel;
    public GameObject NativeObject=>nativeObject;
    public bool VisualHit {get;private set;}
    public void Finish(){if(!nativeObject||!gameObject.activeInHierarchy)return;var classic=nativeObject.GetComponent<ClassicProjectileModel>();if(!classic)return;nativeObject.transform.SetParent(transform.parent,true);Destroy(nativeObject,classic.Finish());nativeObject=null;}
    public void Setup(int kind,Material shell,Material tracer,Material impact) {
        block=new MaterialPropertyBlock();
        if(kind==2||kind==3||kind==5||kind==6||kind==7){body=Sphere(kind==5?"Ensnare missile":kind==6?"Spell steal missile":kind==7?"Acid missile":"Mortar shell",shell);body.localScale=kind==5?new Vector3(.65f,.2f,.65f):Vector3.one*.48f;}
        flash=Sphere("Impact",impact);
        streak=new GameObject("Shot trail").AddComponent<LineRenderer>();streak.transform.SetParent(transform);
        streak.sharedMaterial=tracer;streak.useWorldSpace=true;streak.shadowCastingMode=ShadowCastingMode.Off;streak.numCornerVertices=2;
    }
    Transform Sphere(string name,Material material) {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=name;obj.transform.SetParent(transform);Destroy(obj.GetComponent<Collider>());
        var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return obj.transform;
    }
    public static Vector3 Position(HumanMatch.Shot s,float t,Func<Vector2,Vector3> world) {
        var start=world(s.start)+Vector3.up*s.startHeight;var end=world(s.end)+Vector3.up*s.endHeight;
        return Vector3.Lerp(start,end,t)+Vector3.up*(4*t*(1-t)*Vector2.Distance(s.start,s.end)*s.arc);
    }
    Vector3 DrawPosition(HumanMatch.Shot s,float t,Func<Vector2,Vector3> world){
        var end=Vector2.Lerp(previousEnd,currentEnd,visualBlend);var start=world(s.start)+Vector3.up*s.startHeight;
        return Vector3.Lerp(start,world(end)+Vector3.up*Mathf.Lerp(previousHeight,currentHeight,visualBlend),t)+Vector3.up*(4*t*(1-t)*Vector2.Distance(s.start,end)*s.arc);
    }
    public void Draw(HumanMatch.Shot s,Func<Vector2,Vector3> world,float behind,bool nativeFx=false,GameObject prefab=null,Func<HumanMatch.Shot,PcShotView,bool> drawHd=null) {
        if(observed!=s){observed=s;observedAge=s.age;previousEnd=currentEnd=s.end;previousHeight=currentHeight=s.endHeight;previousDuration=currentDuration=s.duration;hitObserved=s.hit;VisualHit=false;}
        else if(observedAge!=s.age){
            observedAge=s.age;previousEnd=currentEnd;previousHeight=currentHeight;previousDuration=currentDuration;
            if(!hitObserved){currentEnd=s.end;currentHeight=s.endHeight;currentDuration=s.duration;hitObserved=s.hit;}
        }
        visualBlend=Mathf.Clamp01(1-behind/.05f);
        float age=Mathf.Max(0,s.age-behind),duration=Mathf.Max(.0001f,Mathf.Lerp(previousDuration,currentDuration,visualBlend)),t=Mathf.Clamp01(age/duration);
        bool hit=s.hit&&(VisualHit||age+.00001f>=duration);
        VisualHit=hit;
        if(hit)t=1;
        transform.position=DrawPosition(s,t,world);
        if(drawHd?.Invoke(s,this)==true){
            prefab=null;
            nativeFx=true;
        }
        if(prefab&&s.kind!=4){
            if(prefab!=nativePrefab){if(nativeObject)Destroy(nativeObject);nativeObject=Instantiate(prefab,transform);nativeModel=nativeObject.GetComponent<PcModel>();nativePrefab=prefab;}
            if(body)body.gameObject.SetActive(false);flash.gameObject.SetActive(false);streak.gameObject.SetActive(false);nativeObject.SetActive(true);
            nativeObject.transform.position=DrawPosition(s,s.kind==1?1:t,world);
            if(nativeModel){nativeModel.Face(s.end-s.start,true);nativeModel.Play("Stand");}
            var classic=nativeObject.GetComponent<ClassicProjectileModel>();if(classic){var velocity=DrawPosition(s,Mathf.Min(1,t+.005f),world)-DrawPosition(s,Mathf.Max(0,t-.005f),world);classic.Face(velocity);classic.Play(hit&&s.kind!=1);}
            return;
        }
        if(nativeObject)nativeObject.SetActive(false);streak.gameObject.SetActive(true);
        if(s.kind==1||s.kind==4) {
            streak.positionCount=2;streak.startWidth=.08f;streak.endWidth=.025f;
            streak.SetPosition(0,Position(s,0,world));streak.SetPosition(1,Position(s,1,world));
            if(s.kind==4){streak.positionCount=9;for(int i=0;i<9;i++){var p=Vector3.Lerp(Position(s,0,world),Position(s,1,world),i/8f);p+=Vector3.up*(i==0||i==8?0:Mathf.Sin(i*7.2f+s.id)*.35f);streak.SetPosition(i,p);}streak.startWidth=.15f;}
            block.SetColor("_Color",new Color(s.kind==4?.5f:1,s.kind==4?.8f:.8f,s.kind==4?1:.36f,Mathf.Clamp01(1-age/s.duration)));streak.SetPropertyBlock(block);
            flash.position=Position(s,1,world);flash.localScale=Vector3.one*(.25f+.25f*t);flash.gameObject.SetActive(t<1);
        } else {
            if(prefab&&prefab!=nativePrefab){if(nativeModel)Destroy(nativeModel.gameObject);nativeModel=Instantiate(prefab,transform).GetComponent<PcModel>();nativePrefab=prefab;}
            if(nativeModel){nativeModel.gameObject.SetActive(!hit&&prefab);if(!hit&&prefab){nativeModel.transform.position=DrawPosition(s,t,world);nativeModel.Face(s.end-s.start);nativeModel.Play("Stand");}}
            body.gameObject.SetActive(!hit&&!nativeFx&&!prefab);streak.gameObject.SetActive(!hit&&!nativeFx&&!prefab);body.position=DrawPosition(s,t,world);
            if(s.kind==5||s.kind==6||s.kind==7){block.SetColor("_Color",s.kind==7?new Color(.4f,1,.2f):s.kind==6?new Color(.8f,.45f,1):new Color(.25f,.8f,1));body.GetComponent<Renderer>().SetPropertyBlock(block);}
            streak.positionCount=8;streak.startWidth=.025f;streak.endWidth=.22f;
            for(int i=0;i<8;i++)streak.SetPosition(i,DrawPosition(s,Mathf.Lerp(t,Mathf.Max(0,t-.15f/s.duration),i/7f),world));
            block.SetColor("_Color",new Color(.75f,.7f,.6f,.32f));streak.SetPropertyBlock(block);
            flash.gameObject.SetActive(hit);flash.position=DrawPosition(s,1,world);
            float impact=Mathf.Clamp01((age-duration)/.35f);flash.localScale=Vector3.one*(.4f+impact*3);
            block.SetColor("_Color",new Color(1,.5f,.08f,1-impact));flash.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
}
