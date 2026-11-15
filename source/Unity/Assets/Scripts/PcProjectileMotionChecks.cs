using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    JObject MotionRow(int id,string path,float x,float stamp,int team=0)=>new JObject{["id"]=id,["k"]="missile",["path"]=path,["p"]=new JArray(x,World(new Vector2(-148,29)).y+3,29),["origin"]=new JArray(-156,World(new Vector2(-148,29)).y+3,29),["born"]=0,["t"]=team,["yaw"]=Mathf.PI/2,["pitch"]=.1f,["s"]=new JArray(1,1,1)};
    IEnumerator ExerciseProjectileMotion024(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"motion024"));Directory.CreateDirectory(folder);MainMenu();
        var history=new ProjectileMotion();history.Push(Vector3.zero,Quaternion.Euler(0,350,0),0);history.Push(Vector3.right,Quaternion.Euler(0,10,0),.1f);
        Check(Vector3.Distance(history.Sample(.05f).position,Vector3.right*.5f)<.0001f,"Two network samples interpolate halfway at the correct server time");
        Check(Quaternion.Angle(history.Sample(.05f).rotation,Quaternion.identity)<.01f,"Yaw interpolates through the short wrap across zero");
        Check(history.Sample(10).position.x<=2.001f,"Prediction remains bounded to 100 ms during missing packets");
        history.Push(Vector3.right*2,Quaternion.identity,.1f);Check(history.Count==2&&history.Sample(.1f).position.x==2,"Duplicate timestamp replaces a sample without dividing by zero");
        for(int i=2;i<25;i++)history.Push(Vector3.right*i,Quaternion.identity,i*.1f);Check(history.Count==8,"Each projectile retains at most eight samples");
        history.Push(Vector3.zero,Quaternion.identity,0);Check(history.Count==1,"A new round resets backwards server time");
        aspectsOn=false;raceChoice=botRaceChoice=0;matchMode=0;NewMatch();testHoldAuthority=true;options.hdTextures=false;networkSlot=0;RenderMatch();controls.FocusWorld(World(new Vector2(-148,29)),35);
        Check(classicProjectileIndex!=null,"Actual portable loads the native classic projectile bundle");
        string[] keys=((JObject)classicProjectileIndex["weapons"]).Properties().Select(p=>(string)p.Value).Distinct().Take(6).ToArray();string path="native:"+keys[0];
        for(int i=0;i<keys.Length;i++){
            ClearOnlineProjectiles();var row=MotionRow(900000+i,"native:"+keys[i],-155,.1f);DrawOnlineProjectiles(new JArray(row),.1f);
            Check(onlineProjectiles[900000+i].GetComponent<ClassicProjectileModel>(),"Retains native art for smoothed projectile "+keys[i]);
            UpdateOnlineProjectiles(.1f);DrawOnlineProjectiles(new JArray(MotionRow(900000+i,"native:"+keys[i],-154,.2f)),.2f);float before=onlineProjectiles[900000+i].transform.position.x;
            UpdateOnlineProjectiles(1f/60);float after=onlineProjectiles[900000+i].transform.position.x;
            Check(after>before&&after-before<.3f,"Native projectile advances between snapshots "+keys[i]);
        }
        ClearOnlineProjectiles();networkSlot=1;DrawOnlineProjectiles(new JArray(MotionRow(900100,path,-155,.1f)),.1f);Check(onlineProjectiles[900100].transform.position.x>0,"Guest slot mirrors launch origin and flight history consistently");ClearOnlineProjectiles();networkSlot=0;
        DrawOnlineProjectiles(new JArray(MotionRow(900101,path,-155,.1f)),.1f);Check(!onlineProjectiles[900101].activeSelf,"A new projectile stays hidden before its server birth");UpdateOnlineProjectiles(.04f);Check(onlineProjectiles[900101].activeSelf,"Projectile appears when presentation reaches its birth");
        ClearOnlineProjectiles();DrawOnlineProjectiles(new JArray(MotionRow(900102,path,-155,.1f)),.1f);DrawOnlineProjectiles(new JArray(MotionRow(900102,path,-154,.2f)),.2f);
        var ending=new JObject{["kind"]="missile_end",["id"]=900102,["time"]=.28f,["p"]=new JArray(-153,World(new Vector2(-148,29)).y+3,29)};
        DrawOnlineProjectiles(new JArray(),.3f,new JArray(ending));Check(onlineProjectiles.ContainsKey(900102),"Missing snapshot row does not cut off buffered projectile flight");
        var terminal=projectileTracks[900102].motion.Sample(.28f);Check(Mathf.Abs(terminal.position.x+153)<.001f,"Server impact event supplies the exact final point");
        for(int i=0;i<40;i++)UpdateOnlineProjectiles(1f/60);Check(!onlineProjectiles.ContainsKey(900102)&&projectileTracks.Count==0,"Buffered impact retires both view and motion history");
        ClearOnlineProjectiles();DrawOnlineProjectiles(new JArray(MotionRow(900103,path,-155,.1f)),.1f);DrawOnlineProjectiles(new JArray(),.2f);for(int i=0;i<40;i++)UpdateOnlineProjectiles(1f/60);Check(onlineProjectiles.Count==0,"Older snapshots without impact events still clean up boundedly");
        var prefab=ClassicProjectile(keys[0],0);var instance=Instantiate(prefab,visuals.transform);yield return null;var particles=instance.GetComponentsInChildren<ParticleSystem>();float tail=instance.GetComponent<ClassicProjectileModel>().Finish();
        Check(tail>=0&&tail<=1.5f,"Impact bounds its native Death and particle tail lifetime");Destroy(instance);
        var shot=new HumanMatch.Shot{id=990001,team=0,kind=3,start=new Vector2(-156,29),end=new Vector2(-146,29),duration=1,age=.5f,arc=.2f};var view=new GameObject("Offline smooth homing QA").AddComponent<PcShotView>();view.transform.SetParent(visuals.transform);view.Setup(3,shellMaterial,tracerMaterial,impactMaterial);view.Draw(shot,World,0,false,prefab);Vector3 previous=view.transform.position;
        shot.age=.55f;shot.end+=Vector2.up*3;view.Draw(shot,World,.05f,false,prefab);Check(Vector3.Distance(previous,view.transform.position)<.001f,"Moving homing target does not snap the visual endpoint at a simulation tick");
        view.Draw(shot,World,.025f,false,prefab);Check(Vector3.Distance(previous,view.transform.position)>.01f,"Offline homing advances smoothly within the next simulation tick");
        shot.age=1;shot.hit=true;view.Draw(shot,World,.04f,false,prefab);Check(!view.VisualHit,"Offline native death waits until the rendered flight reaches impact");view.Draw(shot,World,0,false,prefab);Check(view.VisualHit,"Offline native impact activates at the final displayed position");
        Vector3 landed=view.transform.position;shot.age=1.05f;shot.end+=Vector2.up*5;shot.duration=1.4f;view.Draw(shot,World,.05f,false,prefab);Check(view.VisualHit&&Vector3.Distance(landed,view.transform.position)<.001f,"Impact stays at its hit position even if authority retains a homing shot while its target moves");Destroy(view.gameObject);
        var frames=new JArray();ClearOnlineProjectiles();int step=0;float began=Time.realtimeSinceStartup,lastX=-156,lastTime=0;int moving=0,active=0;float maxSpeed=0;bool backwards=false;
        int oldRate=Application.targetFrameRate,oldSync=QualitySettings.vSyncCount;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        while(Time.realtimeSinceStartup-began<1.5f){
            float now=Time.realtimeSinceStartup-began;
            while(step<14&&now>=step*.1f+(step%3==0?.025f:0)){
                float stamp=step*.1f;DrawOnlineProjectiles(new JArray(MotionRow(900200,path,-156+stamp*10,stamp)),stamp);step++;
            }
            if(onlineProjectiles.TryGetValue(900200,out var obj)&&obj.activeSelf){
                float x=obj.transform.position.x,dt=now-lastTime;
                if(now>.25f&&dt>0){active++;if(x-lastX>.0001f)moving++;if(x<lastX-.001f)backwards=true;maxSpeed=Mathf.Max(maxSpeed,(x-lastX)/dt);}
                lastX=x;lastTime=now;frames.Add(new JObject{["t"]=now,["x"]=x});
            }
            yield return null;
        }
        Application.targetFrameRate=oldRate;QualitySettings.vSyncCount=oldSync;
        Check(active>15&&moving>active*.8f&&!backwards&&maxSpeed<18,"Real render frames move continuously under 10 Hz snapshots with 25 ms jitter");
        yield return Shot(folder,"01-smoothed-flight");ClearOnlineProjectiles();Check(onlineProjectiles.Count==0&&projectileTracks.Count==0&&!projectileClock,"Match cleanup releases all projectile histories and resets presentation time");
        MainMenu();var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["frames"]=frames,["movingFrames"]=moving,["activeFrames"]=active,["maxSpeed"]=maxSpeed,["androidDeviceTest"]=false};File.WriteAllText(Path.Combine(folder,"motion024-verification.json"),result.ToString());Debug.Log("CF_MOTION024_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
