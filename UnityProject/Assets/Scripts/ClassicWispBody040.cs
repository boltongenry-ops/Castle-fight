using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
// The extracted classic Wisp is made of flat glow layers. A small lit core
// provides depth while its original particles still supply the outer glow.
public sealed class ClassicWispBody040 : MonoBehaviour {
    static Mesh sphere;
    static Material material;
    static int nextPhase;
    static readonly int Tint=Shader.PropertyToID("_Tint");
    Transform core,first,second;
    bool working;
    float phase;
    public Vector3 Center=>core?core.position:transform.position+Vector3.up*.77f;

    public static bool IsWisp(string raw)=>raw=="h051"||raw=="h06B";

    void Awake(){
        var shader=Shader.Find("CastleFight/NativeUnit");
        if(!shader)return;
        if(!material)material=new Material(shader){name="Classic Wisp shared material",hideFlags=HideFlags.HideAndDontSave};
        if(!sphere){
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            primitive.SetActive(false);
            sphere=primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
        }
        bool east=name.Contains("-t1");
        var bodyColor=east?new Color(1,.48f,.30f):new Color(.44f,.98f,.79f);
        var moteColor=east?new Color(1,.82f,.50f):new Color(.65f,.96f,1);
        core=MakeSphere("Wisp core",.52f,bodyColor);
        first=MakeSphere("Wisp orbit A",.14f,moteColor);
        second=MakeSphere("Wisp orbit B",.1f,moteColor);
        phase=(nextPhase++&31)*.196f;
        UpdateOrbit();
    }

    Transform MakeSphere(string name,float diameter,Color color){
        var obj=new GameObject(name){layer=gameObject.layer};
        obj.transform.SetParent(transform,false);
        obj.transform.localScale=Vector3.one*diameter;
        obj.AddComponent<MeshFilter>().sharedMesh=sphere;
        var renderer=obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        var properties=new MaterialPropertyBlock();
        properties.SetColor(Tint,color);
        properties.SetFloat("_Unlit",.33f);
        renderer.SetPropertyBlock(properties);
        return obj.transform;
    }

    public void SetPose(string pose){
        working=pose.IndexOf("Work",System.StringComparison.OrdinalIgnoreCase)>=0;
        enabled=!pose.StartsWith("Death",System.StringComparison.OrdinalIgnoreCase);
        if(core){core.gameObject.SetActive(enabled);first.gameObject.SetActive(enabled);second.gameObject.SetActive(enabled);}
    }

    void LateUpdate()=>UpdateOrbit();

    void UpdateOrbit(){
        if(!core)return;
        float time=Time.time*(working?3.7f:2.35f)+phase;
        var center=new Vector3(0,.77f+Mathf.Sin(time*.64f)*.07f,0);
        core.localPosition=center;
        first.localPosition=center+new Vector3(Mathf.Cos(time)*.38f,Mathf.Sin(time)*.19f,Mathf.Sin(time)*.34f);
        second.localPosition=center-new Vector3(Mathf.Cos(time)*.38f,Mathf.Sin(time)*.17f,Mathf.Sin(time)*.34f);
    }
}
}
