using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed class NativeMapCamera : MonoBehaviour {
    public TextAsset terrainBytes,terrainMetadata,waterMetadata;
    public GameObject waterRoot,sceneryRoot,buildGrid;public Material waterMaterial;
    NativeMapTerrain terrain;Camera view;Vector3 target,shown;float distance=2000f/30f,shownDistance;bool top,locked;
    Vector3 previousPointer;string activeView="west";GUIStyle style;float age;
    float pinch;int panFinger=-1;bool pinchAccepted;
    bool gateAngle;Vector3 gateOffset;
    public bool InputBlocked,HideToolbar,ReserveBuildKeys;public float CameraSpeed=1;
    public bool EdgePan;
    public static Vector2 EdgeMotion(Vector2 cursor,float width,float height){
        if(width<=0||height<=0||cursor.x<0||cursor.y<0||cursor.x>width||cursor.y>height)return Vector2.zero;
        float band=Mathf.Clamp(height*.025f,14,32);
        float x=cursor.x<band?-Mathf.Clamp01((band-cursor.x)/band):cursor.x>width-band?Mathf.Clamp01((cursor.x-width+band)/band):0;
        float y=cursor.y<band?-Mathf.Clamp01((band-cursor.y)/band):cursor.y>height-band?Mathf.Clamp01((cursor.y-height+band)/band):0;
        return Vector2.ClampMagnitude(new Vector2(x,y),1);
    }
    public Func<Vector2,bool> TouchOverUi;public Func<bool> TouchPanAllowed;
    public bool AllowGridToggle=true;
    public NativeMapTerrain Terrain=>terrain;
    void Start() {
        enabled=false;
        if(!terrainBytes||!terrainMetadata||!waterMetadata)throw new InvalidOperationException("Map terrain assets are not assigned");
        terrain=new NativeMapTerrain(terrainBytes.bytes,JObject.Parse(terrainMetadata.text),(JObject)JObject.Parse(waterMetadata.text)["water"]);
        view=GetComponent<Camera>();view.fieldOfView=50;view.nearClipPlane=.5f;view.farClipPlane=1200;
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        Focus(BattleLab.Arg("--map-view","west"));
        string time=BattleLab.Arg("--water-time");if(time!="")waterMaterial.SetFloat("_CaptureTime",float.Parse(time,CultureInfo.InvariantCulture));else waterMaterial.SetFloat("_CaptureTime",-1);
        string shot=BattleLab.Arg("--map-shot");if(shot!=""){locked=true;StartCoroutine(Capture(shot));}
        Debug.Log("CF_FULL_MAP_READY view="+activeView);
        enabled=true;
    }
    public void Focus(string preset) {
        activeView=preset;top=preset=="top";view.orthographic=top;gateAngle=false;
        if(top){view.orthographicSize=140;transform.position=new Vector3(0,500,0);transform.LookAt(new Vector3(0,10,0),Vector3.forward);return;}
        target=terrain.Position(-4544,448);distance=2000f/30;
        if(preset=="east")target=terrain.Position(4544,448);
        if(preset=="west-gate"){target=terrain.Position(-2016,0);distance=58;gateAngle=true;gateOffset=new Vector3(55,11,34);}
        if(preset=="east-gate"){target=terrain.Position(2016,0);distance=58;gateAngle=true;gateOffset=new Vector3(-55,11,34);}
        if(preset=="river"){target=terrain.Position(0,0);distance=85;}
        if(preset=="north"){target=terrain.Position(0,-2700);distance=95;}
        if(preset=="south"){target=terrain.Position(0,2700);distance=95;}
        if(preset=="castle"){target=terrain.Position(-5120,64);distance=70;}
        if(preset=="fire"){
            foreach(var p in UnityEngine.Object.FindObjectsByType<NativePlacement>(FindObjectsSortMode.None))if(p.rawcode=="ZOfp"){target=p.transform.position;distance=15;break;}
        }
        shown=target;shownDistance=distance;Apply();
    }
    void Apply(){float pitch=56*Mathf.Deg2Rad;transform.position=shown+(gateAngle?gateOffset*shownDistance/58:new Vector3(0,shownDistance*Mathf.Sin(pitch),-shownDistance*Mathf.Cos(pitch)));transform.LookAt(shown);}
    public void GameplayViewport(bool active) {
        if(!view)return;
        // Keep rendering behind the console's transparent leaves. Shift the optical
        // centre into the playable area without leaving any uncleared screen pixels.
        view.rect=new Rect(0,0,1,1);view.ResetProjectionMatrix();
        if(!active||top)return;
        Rect safe=Application.isMobilePlatform?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height);float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
        float centreFromTop=Screen.height-safe.yMax+(safe.height-720*scale)/2+274*scale;
        var projection=Matrix4x4.Perspective(view.fieldOfView,view.aspect,view.nearClipPlane,view.farClipPlane);
        projection[1,2]=2*centreFromTop/Screen.height-1;view.projectionMatrix=projection;
    }
    public void FocusWorld(Vector3 point,float zoom=45){top=false;view.orthographic=false;gateAngle=false;target=shown=point;distance=shownDistance=zoom;Apply();}
    public void PanToWorld(Vector3 point){target=shown=point;if(top){FocusWorld(point,85);return;}Apply();}
    void Update() {
        age+=Time.deltaTime;
        if(BattleLab.Arg("--auto-quit")=="1"&&age>float.Parse(BattleLab.Arg("--map-seconds","7"),CultureInfo.InvariantCulture))Application.Quit();
        if(locked||InputBlocked){shown=target;return;}
        if(Input.GetKeyDown(KeyCode.Home))Focus("west");
        if(AllowGridToggle&&Input.GetKeyDown(KeyCode.G))buildGrid.SetActive(!buildGrid.activeSelf);
        if(top)return;
        var motion=Vector3.zero;
        if((!ReserveBuildKeys&&Input.GetKey(KeyCode.W))||Input.GetKey(KeyCode.UpArrow))motion.z+=1;
        if((!ReserveBuildKeys&&Input.GetKey(KeyCode.S))||Input.GetKey(KeyCode.DownArrow))motion.z-=1;
        if((!ReserveBuildKeys&&Input.GetKey(KeyCode.A))||Input.GetKey(KeyCode.LeftArrow))motion.x-=1;
        if((!ReserveBuildKeys&&Input.GetKey(KeyCode.D))||Input.GetKey(KeyCode.RightArrow))motion.x+=1;
        var cursor=Input.mousePosition;
        if(EdgePan&&Application.isFocused){var edge=EdgeMotion(cursor,Screen.width,Screen.height);motion+=new Vector3(edge.x,0,edge.y);}
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        motion=Vector3.ClampMagnitude(motion,1);target+=motion*(55*Mathf.Clamp(distance/55,.65f,2)*dt*CameraSpeed);
        // Stop as soon as the cursor leaves the edge; no camera coasting.
        if(motion.sqrMagnitude>0)shown=target;
        if(Input.GetMouseButtonDown(2))previousPointer=Input.mousePosition;
        if(Input.GetMouseButton(2)){var delta=Input.mousePosition-previousPointer;target+=new Vector3(-delta.x,0,-delta.y)*(.06f*distance/55);previousPointer=Input.mousePosition;}
        float wheel=TouchOverUi!=null&&TouchOverUi(cursor)?0:Input.mouseScrollDelta.y;if(wheel!=0)distance=Mathf.Clamp(distance/Mathf.Pow(1.12f,wheel),10,220);
        if(Input.touchCount==1){var touch=Input.GetTouch(0);if(touch.phase==TouchPhase.Began)panFinger=TouchOverUi!=null&&TouchOverUi(touch.position)?-1:touch.fingerId;if(touch.fingerId==panFinger&&touch.phase==TouchPhase.Moved&&(TouchPanAllowed==null||TouchPanAllowed()))target+=new Vector3(-touch.deltaPosition.x,0,-touch.deltaPosition.y)*(.09f*distance/55*CameraSpeed);if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)panFinger=-1;pinch=0;pinchAccepted=false;}
        else if(Input.touchCount>=2){var a=Input.GetTouch(0);var b=Input.GetTouch(1);if(pinch<=0)pinchAccepted=(TouchOverUi==null||(!TouchOverUi(a.position)&&!TouchOverUi(b.position)));float gap=Vector2.Distance(a.position,b.position);if(pinchAccepted&&pinch>0)distance=Mathf.Clamp(distance*pinch/Mathf.Max(1,gap),10,220);pinch=gap;panFinger=-1;}else{pinch=0;pinchAccepted=false;panFinger=-1;}
        target.x=Mathf.Clamp(target.x,-6144f/30,6144f/30);target.z=Mathf.Clamp(target.z,-3584f/30,3584f/30);
        target.y=terrain.Height(target.x*30,-target.z*30);
        shown=Vector3.Lerp(shown,target,1-Mathf.Exp(-14*Time.deltaTime));shownDistance=Mathf.Lerp(shownDistance,distance,1-Mathf.Exp(-12*Time.deltaTime));Apply();
    }
    void OnGUI() {
        if(locked||HideToolbar)return;
        if(style==null)style=new GUIStyle(GUI.skin.label){fontSize=15};
        GUI.Box(new Rect(16,16,670,86),GUIContent.none);
        string[] names={"Обзор","Запад","Восток","Река","Ворота","Замок"};string[] ids={"top","west","east","river","west-gate","castle"};
        for(int i=0;i<names.Length;i++)if(GUI.Button(new Rect(28+i*107,28,100,30),names[i]))Focus(ids[i]);
        GUI.Label(new Rect(28,65,650,25),"WASD / стрелки — движение · колесо — масштаб · средняя кнопка — сдвиг · G — сетка",style);
    }
    IEnumerator Capture(string path) {
        yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;view.targetTexture=rt;view.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
        view.targetTexture=null;RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(texture);
        Debug.Log("CF_MAP_CAPTURE_PASS "+activeView+" "+path);
        int particles=0,torchParticles=0;
        foreach(var e in UnityEngine.Object.FindObjectsByType<NativeMapEmitter>(FindObjectsSortMode.None)){particles+=e.emitter.particleCount;if(e.sourceProfile.Contains("torchhumanomni"))torchParticles+=e.emitter.particleCount;}
        Debug.Log("CF_MAP_PARTICLES total="+particles+" torches="+torchParticles);
        foreach(var e in UnityEngine.Object.FindObjectsByType<NativeMapEmitter>(FindObjectsSortMode.None))if(e.sourceProfile.Contains("zofp_0_firepot")){
            var values=new ParticleSystem.Particle[16];int count=e.emitter.GetParticles(values);
            if(count==0)continue;
            Debug.Log("CF_MAP_FIRECHECK origin="+e.transform.position+" scale="+e.transform.lossyScale+" count="+count+" particle="+values[0].position+" world="+e.transform.TransformPoint(values[0].position)+" velocity="+e.transform.TransformDirection(values[0].totalVelocity)+" size="+values[0].GetCurrentSize(e.emitter)+" color="+values[0].GetCurrentColor(e.emitter));break;
        }
    }
}
}
