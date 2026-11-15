using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFight {
// A rendering/CPU load prototype. Full authority remains the Godot 0.6.17 server.
public sealed class BattleLab : MonoBehaviour {
    public GameObject[] models;
    public Texture2D[] groundTextures;
    public Material groundMaterial;
    public Material particleMaterial;
    public int unitCount = 300;
    sealed class Actor {
        public Transform root; public Animation animation; public Animator animator;
        public NativeAppearance appearance;
        public Dictionary<string,AnimationClip> clips = new Dictionary<string,AnimationClip>();
        public Vector3 previous, position; public float hp, cooldown, deathTime, pending, deathLength=2;
        public int team, target=-1; public string state="";
    }
    readonly List<Actor> actors = new List<Actor>();
    readonly Dictionary<Vector2Int,List<int>> grid = new Dictionary<Vector2Int,List<int>>();
    readonly Stack<List<int>> buckets = new Stack<List<int>>();
    readonly List<float> frames = new List<float>(40000);
    JObject traits; Camera view; float[] heights; byte[] terrain;
    int width, height, tick, kills; float accumulator, warmup=10, duration=60, elapsed, average;
    const float Step=.05f, Cell=10;
    string resultDirectory, status="Warming up", shot; bool autoQuit, exported, captureRequested;
    long gcStart; int collections; double simMs; GUIStyle title, label; bool overview;
    bool fxEnabled;
    string inspectionPose;

    public static string Arg(string key,string fallback="") {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length;i++) {
            if(args[i].StartsWith(key+"=")) return args[i].Substring(key.Length+1);
            if(args[i]==key && i+1<args.Length) return args[i+1];
        }
        return fallback;
    }
    void Start() {
        Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
        QualitySettings.shadows=ShadowQuality.Disable; QualitySettings.antiAliasing=0;
        Time.maximumDeltaTime=.25f;
        unitCount=Mathf.Clamp(int.Parse(Arg("--units",unitCount.ToString())),2,1000);
        inspectionPose=Arg("--pose");
        warmup=float.Parse(Arg("--warmup","10"),CultureInfo.InvariantCulture);
        duration=float.Parse(Arg("--duration","60"),CultureInfo.InvariantCulture);
        autoQuit=Arg("--auto-quit")=="1"; shot=Arg("--screenshot");
        resultDirectory=Arg("--results",Path.Combine(Application.persistentDataPath,"benchmarks",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(resultDirectory);
        traits=JObject.Parse(ContentResources.Load<TextAsset>("benchmark-traits").text);
        BuildTerrain(); SetupCamera(); Spawn(); ResetMeasurement();
        fxEnabled=Arg("--fx","0")=="1";
        if(fxEnabled) {var fx=new GameObject("Native WC3 FX preview").AddComponent<NativeFxPreview>();fx.particleMaterial=particleMaterial;}
        if(!string.IsNullOrEmpty(shot)) StartCoroutine(Capture());
        Debug.Log("CF_LAB_READY units="+unitCount+" results="+resultDirectory);
    }
    IEnumerator Capture() {
        yield return new WaitForSeconds(3);
        yield return new WaitForEndOfFrame();
        // An explicit render target also works when the desktop player window is hidden.
        var target=new RenderTexture(1280,720,24);view.targetTexture=target;view.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes(shot,image.EncodeToPNG());
        view.targetTexture=null;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(image);
        captureRequested=true;
    }
    void ResetMeasurement() {
        elapsed=0; exported=false; frames.Clear(); gcStart=GC.GetTotalMemory(false);
        collections=GC.CollectionCount(0); status="Warming up";
    }
    void SetupCamera() {
        var obj=new GameObject("Benchmark camera"); view=obj.AddComponent<Camera>();
        view.backgroundColor=new Color(.035f,.05f,.055f); view.clearFlags=CameraClearFlags.SolidColor;
        view.nearClipPlane=.3f; view.farClipPlane=900;
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.72f,.75f,.72f);
        RenderSettings.fog=false;
        var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional;
        light.intensity=.8f; light.transform.rotation=Quaternion.Euler(45,-35,0); light.shadows=LightShadows.None;
        SetCamera(false);
    }
    void SetCamera(bool wide) {
        overview=wide; view.orthographic=true; view.orthographicSize=wide?145:48;
        var target=new Vector3(0,Sample(0,0),0);
        view.transform.position=target+new Vector3(wide?110:35,wide?245:90,wide?-185:-72);
        view.transform.LookAt(target);
        if(Arg("--view")=="closeup") {
            view.orthographicSize=6;view.transform.position=target+Quaternion.Euler(0,float.Parse(Arg("--angle","0"),CultureInfo.InvariantCulture),0)*new Vector3(0,7,-14);view.transform.LookAt(target+Vector3.up*1.5f);
        }
    }
    void BuildTerrain() {
        terrain=ContentResources.Load<TextAsset>("terrain").bytes;
        width=BitConverter.ToInt32(terrain,0); height=BitConverter.ToInt32(terrain,4);
        int n=width*height; heights=new float[n]; Buffer.BlockCopy(terrain,8,heights,0,n*4);
        // Source lightweight_map.gd dry_heights: retain the existing water walking plane.
        for(int y=0;y<height-1;y++)for(int x=0;x<width-1;x++) {
            int p=y*width+x;int[] corners={p,p+1,p+width,p+width+1};
            if(corners.Any(c=>(terrain[8+n*9+c]&0x40)!=0))
                foreach(int c in corners)heights[c]=Mathf.Max(heights[c],BitConverter.ToSingle(terrain,8+n*4+c*4)-76.8f);
        }
        for(int pass=0;pass<groundTextures.Length*2;pass++) {
            int g=pass%groundTextures.Length;bool overlay=pass>=groundTextures.Length;
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var indices=new List<int>();
            for(int y=0;y<height-1;y++) for(int x=0;x<width-1;x++) {
                float wx=-8192+x*128, wy=-4096+y*128;
                if(wx < -6144 || wx+128>6144 || wy < -3584 || wy+128>3584) continue;
                int point=y*width+x;
                int[] corners={point,point+1,point+width+1,point+width};
                int[] ids=corners.Select(c=>Mathf.Min(terrain[8+n*8+c]&15,8)).ToArray();
                var layers=ids.Distinct().OrderBy(id=>id).ToArray();
                int rank=Array.IndexOf(layers,g);
                if(rank<0 || (overlay?rank==0:rank!=0))continue;
                int v=vertices.Count;
                int variation=terrain.Length>=8+n*11?terrain[8+n*10+point]:0;
                int cols=groundTextures[g].width/64, rows=groundTextures[g].height/64;
                int cx=cols>rows ? (variation<16?4+variation%4:variation==16?3:0) : (variation==0?0:3);
                int cy=cols>rows ? (variation<16?variation/4:variation==16?3:0) : (variation==0?0:3);
                if(overlay){int mask=0;int[] bits={2,1,4,8};for(int k=0;k<4;k++)if(ids[k]==g)mask|=bits[k];cx=mask%4;cy=mask/4;}
                for(int k=0;k<4;k++) {
                    int dx=(k==1||k==2)?1:0, dy=k>=2?1:0;
                    // Godot (x,y,z) -> Unity (x,y,-z); WC3 heights use the same 1/30 scale.
                    vertices.Add(new Vector3((wx+dx*128)/30,heights[corners[k]]/30+rank*.002f,-(wy+dy*128)/30));
                    uv.Add(new Vector2((cx*64+.5f+dx*63)/groundTextures[g].width,1-(cy*64+.5f+(1-dy)*63)/groundTextures[g].height));
                }
                indices.AddRange(new[]{v,v+1,v+2,v,v+2,v+3});
            }
            var mesh=new Mesh {name="WC3 ground "+g,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var obj=new GameObject(mesh.name); obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(groundMaterial); mat.mainTexture=groundTextures[g];mat.SetFloat("_Layer",overlay?1:0);mat.renderQueue=overlay?2450+g:2000;
            obj.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
    }
    float Sample(float x,float z) {
        float fx=Mathf.Clamp((x*30+8192)/128,0,width-1.001f), fy=Mathf.Clamp((-z*30+4096)/128,0,height-1.001f);
        int ix=(int)fx, iy=(int)fy, i=iy*width+ix; float a=fx-ix,b=fy-iy;
        return Mathf.Lerp(Mathf.Lerp(heights[i],heights[i+1],a),Mathf.Lerp(heights[i+width],heights[i+width+1],a),b)/30;
    }
    void Spawn() {
        foreach(var a in actors) Destroy(a.root.gameObject); actors.Clear();
        for(int i=0;i<unitCount;i++) {
            int team=i%2;
            var root=new GameObject("Unit "+i).transform;
            var model=Instantiate(models[team],root);
            // Explicit legacy playback keeps the imported clips; no generated humanoid retargeting.
            var animator=model.GetComponentInChildren<Animator>(); if(animator) animator.enabled=false;
            var anim=model.GetComponent<Animation>(); if(!anim) anim=model.AddComponent<Animation>();
            var actor=new Actor {root=root,animation=anim,team=team,animator=animator};
            foreach(var clip in RuntimeClips[team]) {anim.AddClip(clip,clip.name); actor.clips[clip.name]=clip;}
            actor.appearance=model.AddComponent<NativeAppearance>();actor.appearance.Configure(team==0?"hfoo":"o002",anim);
            if(actor.clips.Count==0) throw new InvalidOperationException("No clips on imported model "+team);
            anim.cullingType=AnimationCullingType.BasedOnRenderers;
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) { renderer.updateWhenOffscreen=false; renderer.shadowCastingMode=ShadowCastingMode.Off; }
            actors.Add(actor); Respawn(actor,i);
            if(!string.IsNullOrEmpty(inspectionPose)) {actor.position=new Vector3(team==0?-3:3,Sample(team==0?-3:3,0),0);actor.previous=actor.position;actor.root.position=actor.position;actor.root.rotation=Quaternion.Euler(0,180,0);}
            Play(actor,string.IsNullOrEmpty(inspectionPose)?"Walk":inspectionPose);
        }
        tick=0; kills=0; accumulator=0;
    }
    // Populated by the scene builder, so every clip remains referenced in player builds.
    public AnimationClip[] blueClips, redClips;
    AnimationClip[][] RuntimeClips => new[]{blueClips,redClips};
    JObject Stats(Actor a) => (JObject)traits[a.team==0?"hfoo":"o002"];
    float Stat(Actor a,string key) => (float)Stats(a)[key];
    void Respawn(Actor a,int i) {
        int rank=i/2; float x=(a.team==0?-1:1)*(12+(rank/18)*2.4f), z=(rank%18-8.5f)*2.5f;
        a.position=new Vector3(x,Sample(x,z),z); a.previous=a.position; a.root.position=a.position;
        a.root.localScale=Vector3.one;
        a.hp=Stat(a,"hp"); a.cooldown=(i%17)*.04f; a.deathTime=0; a.pending=0; a.target=-1;
    }
    void Play(Actor a,string requested) {
        if(a.state==requested) return;
        var clip=a.clips.Values.FirstOrDefault(c=>string.Equals(c.name,requested,StringComparison.OrdinalIgnoreCase))
            ?? a.clips.Values.FirstOrDefault(c=>c.name.StartsWith(requested,StringComparison.OrdinalIgnoreCase));
        if(clip==null) throw new InvalidOperationException("Missing clip "+requested);
        a.state=requested; var s=a.animation[clip.name];
        if(requested=="Death")a.deathLength=clip.length;
        s.wrapMode=requested=="Death"?WrapMode.ClampForever:WrapMode.Loop;
        s.speed=requested=="Walk"?Stat(a,"speed")*30/(Stat(a,"walk_animation_speed")*Stat(a,"model_scale")):1;
        a.animation.CrossFade(clip.name,.15f);
        a.appearance.SetClip(clip.name);
    }
    void Simulate() {
        foreach(var b in grid.Values) {b.Clear(); buckets.Push(b);} grid.Clear();
        for(int i=0;i<actors.Count;i++) {
            var a=actors[i]; a.previous=a.position;
            if(a.hp<=0) continue;
            var key=new Vector2Int(Mathf.FloorToInt(a.position.x/Cell),Mathf.FloorToInt(a.position.z/Cell));
            if(!grid.TryGetValue(key,out var list)) {list=buckets.Count>0?buckets.Pop():new List<int>(32); grid.Add(key,list);} list.Add(i);
        }
        // Pending damage is applied together, avoiding team/order advantage for this load fixture.
        for(int i=0;i<actors.Count;i++) {
            var a=actors[i];
            if(a.hp<=0) {a.deathTime+=Step;if(a.deathTime>a.deathLength+.1f) {Respawn(a,i); Play(a,"Walk");}continue;}
            a.cooldown-=Step; int target=-1; float best=26.67f*26.67f;
            var cell=new Vector2Int(Mathf.FloorToInt(a.position.x/Cell),Mathf.FloorToInt(a.position.z/Cell));
            for(int x=-3;x<=3;x++) for(int y=-3;y<=3;y++) {
                if(!grid.TryGetValue(cell+new Vector2Int(x,y),out var list)) continue;
                foreach(int j in list) {var b=actors[j];if(b.team==a.team||b.hp<=0)continue;float d=(a.position-b.position).sqrMagnitude;if(d<best){best=d;target=j;}}
            }
            a.target=target;
            Vector3 direction=target>=0?actors[target].position-a.position:new Vector3(a.team==0?1:-1,0,0);
            direction.y=0;
            if(direction.sqrMagnitude>.001f) a.root.rotation=Quaternion.LookRotation(direction);
            if(target>=0 && best<=Stat(a,"range")*Stat(a,"range")) {
                Play(a,"Attack");
                if(a.cooldown<=0) {
                    a.cooldown+=Stat(a,"cooldown");
                    float armor=Stat(actors[target],"armor");
                    actors[target].pending+=Stat(a,"damage")/(1+.06f*armor);
                }
            } else {
                Play(a,"Walk"); a.position+=direction.normalized*(Stat(a,"speed")*Step);
                a.position.y=Sample(a.position.x,a.position.z);
            }
        }
        foreach(var a in actors) {
            if(a.hp>0) {a.hp-=a.pending;if(a.hp<=0){kills++;Play(a,"Death");}}
            a.pending=0;
        }
        tick++;
    }
    void Update() {
        if(actors.Count==0)return;
        float dt=Time.unscaledDeltaTime; elapsed+=dt; accumulator+=dt;
        var sw=System.Diagnostics.Stopwatch.StartNew();
        while(accumulator>=Step) {if(string.IsNullOrEmpty(inspectionPose))Simulate();accumulator-=Step;}
        simMs=sw.Elapsed.TotalMilliseconds;
        float blend=accumulator/Step;
        foreach(var a in actors) a.root.position=Vector3.Lerp(a.previous,a.position,blend);
        average=Mathf.Lerp(average,dt,.05f);
        if(elapsed>=warmup && !exported) {if(frames.Count==0){gcStart=GC.GetTotalMemory(false);collections=GC.CollectionCount(0);}status="Recording";frames.Add(dt*1000);}
        if(elapsed>=warmup+duration && !exported) Export();
        if(autoQuit && exported && (!string.IsNullOrEmpty(shot)?captureRequested:true)) Application.Quit();
    }
    void Export() {
        exported=true; status="Saved";
        var sorted=frames.OrderBy(f=>f).ToArray(); if(sorted.Length==0)return;
        float Percent(float p)=>sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length*p)-1,0,sorted.Length-1)];
        File.WriteAllLines(Path.Combine(resultDirectory,"frames.csv"),new[]{"frame,wall_frame_ms"}.Concat(frames.Select((f,i)=>i+","+f.ToString("F4",CultureInfo.InvariantCulture))));
        var report=new JObject { ["engine"]=Application.unityVersion,["build"]="Unity migration lab 0.1",["sourceBuild"]="0.6.17",
            ["mode"]="synthetic-melee-load-not-gameplay-parity",["units"]=unitCount,["ticks"]=tick,["kills"]=kills,
            ["warmupSeconds"]=warmup,["measureSeconds"]=duration,["samples"]=frames.Count,["resolution"]=Screen.width+"x"+Screen.height,
            ["device"]=SystemInfo.deviceModel,["os"]=SystemInfo.operatingSystem,["gpu"]=SystemInfo.graphicsDeviceName,
            ["graphicsApi"]=SystemInfo.graphicsDeviceType.ToString(),["targetFps"]=Application.targetFrameRate,
            ["medianMs"]=Percent(.5f),["p95Ms"]=Percent(.95f),["p99Ms"]=Percent(.99f),["maxMs"]=sorted.Last(),
            ["framesOver33_3Ms"]=frames.Count(f=>f>33.333f),["framesOver50Ms"]=frames.Count(f=>f>50),
            ["managedBytesDelta"]=GC.GetTotalMemory(false)-gcStart,["gen0Collections"]=GC.CollectionCount(0)-collections,
            ["nativeAllocatedBytes"]=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
            ["utc"]=DateTime.UtcNow.ToString("O"),["shadows"]=false,["effects"]=fxEnabled,["fog"]=false,["water"]=false,
            ["terrainMode"]="native-atlas-corner-masks-dry-walking-plane",["simulationHz"]=20 };
        File.WriteAllText(Path.Combine(resultDirectory,"summary.json"),report.ToString());
        Debug.Log("CF_BENCHMARK "+report.ToString(Newtonsoft.Json.Formatting.None));
    }
    void OnGUI() {
        if(title==null){title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold};label=new GUIStyle(GUI.skin.label){fontSize=16};}
        GUI.Box(new Rect(16,16,390,214),GUIContent.none);
        GUI.Label(new Rect(30,26,360,36),"CASTLE FIGHT / UNITY LAB",title);
        GUI.Label(new Rect(30,65,360,26),$"{unitCount} animated units   |   {(average>0?1/average:0):F0} FPS",label);
        GUI.Label(new Rect(30,91,360,26),$"{status}  {elapsed:F0}/{warmup+duration:F0}s  |  tick {simMs:F1} ms",label);
        GUI.Label(new Rect(30,117,360,26),"Synthetic combat / original WC3 assets",label);
        GUI.Label(new Rect(30,142,360,26),$"FX: {(fxEnabled?"native preview":"OFF")} | Water, fog, shadows: OFF",label);
        for(int i=0;i<3;i++) if(GUI.Button(new Rect(30+i*88,180,80,32),new[]{"100","300","600"}[i])) { unitCount=new[]{100,300,600}[i];Spawn();ResetMeasurement(); }
        if(GUI.Button(new Rect(298,180,90,32),overview?"Battle":"Map")) SetCamera(!overview);
    }
}
}
