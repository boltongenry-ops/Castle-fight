using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    const int FogScreenWidth=160,FogScreenHeight=90,FogMeshWidth=41,FogMeshHeight=24,FogStride=FogGrid.Width+2;
    FogGrid fog;HumanMatch fogMatch;float fogNext,screenFogNext;Texture2D fogScreen,fogMini;Color32[] screenFogColors,miniFogColors;
    readonly byte[] fogDisplay=new byte[FogStride*(FogGrid.Height+2)];
    readonly Vector3[] fogProjectionNodes=new Vector3[FogMeshWidth*FogMeshHeight];
    Vector3 fogCameraPosition;Quaternion fogCameraRotation;Matrix4x4 fogProjection;Rect fogViewport;int fogWidth,fogHeight;bool fogSoft;double fogProjectionMs,fogMaskMs;
    readonly HashSet<int> fogKnownHuts=new HashSet<int>();
    bool FogActive=>match!=null&&(onlineMatch?onlineFog:(activeChallenge!=null||options.fogOfWar));
    bool FogVisible(Vector2 point)=>!FogActive||fog!=null&&fog.Visible(point);
    bool CanSeeUnit(HumanMatch.Unit unit)=>unit!=null&&match.NightCanDetect(0,unit)&&(unit.team==0||FogVisible(unit.position));
    bool CanInspectHut(HumanMatch.Hut hut)=>hut!=null&&(hut.team==0||FogVisible(hut.position));
    void UpdateFog(bool force=false){
        if(match==null)return;
        if(fogMatch!=match){fogMatch=match;fog=new FogGrid();fogKnownHuts.Clear();fogNext=screenFogNext=0;match.VisionTest=(team,point)=>team!=0||FogVisible(point);}
        if(!FogActive)return;if(!force&&Time.unscaledTime<fogNext)return;fogNext=Time.unscaledTime+.18f;fog.Clear();fog.RevealBase();
        fog.Reveal(match.Builders[0].position,800f/30);
        foreach(var unit in match.Units)if(unit.hp>0&&(unit.team==0||match.ElvenReveals(unit,0)))fog.Reveal(unit.position,HumanMatch.F(match.Lines[unit.kind].stats,"sight",800f/30));
        foreach(var hut in match.Huts)if(hut.team==0&&hut.hp>0)fog.Reveal(hut.position,HumanMatch.F(match.Lines[hut.kind].buildingStats,"sight",1000f/30));
        if(onlineMatch)foreach(var point in networkSight)fog.Reveal(point.position,point.radius);
        foreach(var orb in match.NorthernOrbs)if(orb.team==0)fog.Reveal(orb.position,700f/30);
        foreach(var zone in match.NightZones)if(zone.team==0&&zone.age<8)fog.Reveal(zone.point,1100f/30);
        fog.Remember();foreach(var hut in match.Huts)if(hut.team==0||fog.Visible(hut.position))fogKnownHuts.Add(hut.id);
        if(!fogMini){fogMini=new Texture2D(FogGrid.Width,FogGrid.Height,TextureFormat.RGBA32,false){name="Exploration minimap",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};miniFogColors=new Color32[FogGrid.Width*FogGrid.Height];}
        int shade=options.softFog?104:255;
        for(int y=0;y<FogGrid.Height;y++)for(int x=0;x<FogGrid.Width;x++){
            int i=y*FogGrid.Width+x;
            byte display=(byte)Math.Max(fog.Current[i],fog.Explored[i]>0?85:0);
            fogDisplay[(y+1)*FogStride+x+1]=display;
            miniFogColors[i]=new Color32(0,0,0,(byte)((shade-display*shade/255f)*.96f));
        }
        fogMini.SetPixels32(miniFogColors);fogMini.Apply(false);screenFogNext=0;
    }
    void ApplyFogViews(){
        if(match==null)return;
        foreach(var unit in match.Units)if(units.TryGetValue(unit.id,out var view)&&unit.hp>0)view.gameObject.SetActive(CanSeeUnit(unit)&&(match.MechMine(unit)?unit.team==0||unit.mechMineAge<2:!match.Hidden(unit)));
        foreach(var hut in match.Huts)if(huts.TryGetValue(hut.id,out var view))view.gameObject.SetActive(!FogActive||hut.team==0||fogKnownHuts.Contains(hut.id));
        bool castleKnown=!FogActive||fog.Known(HumanMatch.CastlePosition(1));castleObjects[1].SetActive(castleKnown&&!options.hdTextures);if(castleViews[1])castleViews[1].gameObject.SetActive(castleKnown&&options.hdTextures);
        if(builders[1])builders[1].gameObject.SetActive(FogVisible(match.Builders[1].position));
        foreach(var shot in match.Shots)if(projectiles.TryGetValue(shot.id,out var view))view.gameObject.SetActive(!elvenBeams.ContainsKey(shot.id)&&(shot.kind!=4||ShowBeam036(shot))&&FogVisible(new Vector2(view.transform.position.x,-view.transform.position.z)));
        if(onlineMatch)foreach(var pair in onlineProjectiles)if(pair.Value)pair.Value.SetActive(ProjectileBorn(pair.Key)&&FogVisible(new Vector2(pair.Value.transform.position.x,-pair.Value.transform.position.z)));
        if(SelectedUnit==null)selectedUnit=-1;if(SelectedHut==null)selectedHut=-1;
    }
    void DrawWorldFog(){
        if(!FogActive||fog==null)return;var camera=Camera.main;if(!camera)return;
        if(Event.current.type!=EventType.Repaint)return;
        if(!fogScreen){
            fogScreen=new Texture2D(FogScreenWidth,FogScreenHeight,TextureFormat.RGBA32,false){name="Terrain projected fog",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            screenFogColors=new Color32[FogScreenWidth*FogScreenHeight];
        }
        var projection=camera.projectionMatrix;var position=camera.transform.position;var rotation=camera.transform.rotation;var viewport=camera.rect;
        int width=Screen.width,height=Screen.height;
        bool moved=fogWidth!=width||fogHeight!=height||fogProjection!=projection||fogCameraPosition!=position||fogCameraRotation!=rotation||fogViewport!=viewport;
        if(moved||fogSoft!=options.softFog||Time.unscaledTime>=screenFogNext){
            long stamp=System.Diagnostics.Stopwatch.GetTimestamp();
            if(moved){
                ProjectFog(camera,width,height,position);
                fogProjection=projection;fogCameraPosition=position;fogCameraRotation=rotation;fogViewport=viewport;fogWidth=width;fogHeight=height;
                fogProjectionMs=FogElapsed(stamp);
            }
            stamp=System.Diagnostics.Stopwatch.GetTimestamp();
            fogSoft=options.softFog;
            PaintFogMask(fogSoft?104:255);
            fogScreen.SetPixels32(screenFogColors);
            fogScreen.Apply(false);
            screenFogNext=Time.unscaledTime+.18f;
            fogMaskMs=FogElapsed(stamp);
        }
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),fogScreen,ScaleMode.StretchToFill,true);
    }
    static double FogElapsed(long stamp)=>(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    void ProjectFog(Camera camera,int width,int height,Vector3 cameraPosition){
        // Ray origins on the near plane are affine even when the viewport is letterboxed.
        var corner=camera.ScreenPointToRay(Vector3.zero);
        var dx=(camera.ScreenPointToRay(new Vector3(width,0)).origin-corner.origin)/(FogMeshWidth-1);
        var dy=(camera.ScreenPointToRay(new Vector3(0,height*92f/FogScreenHeight)).origin-corner.origin)/(FogMeshHeight-1);
        bool orthographic=camera.orthographic;
        for(int y=0;y<FogMeshHeight;y++){
            var row=corner.origin+dy*y;
            for(int x=0;x<FogMeshWidth;x++){
                var start=row+dx*x;var direction=orthographic?corner.direction:start-cameraPosition;
                float px=start.x,pz=start.z;
                if(direction.y<-.0001f){
                    float xPerY=direction.x/direction.y,zPerY=direction.z/direction.y;
                    float surface=12;
                    for(int i=0;i<6;i++){
                        float rise=Math.Min(0,surface-start.y);
                        px=start.x+rise*xPerY;pz=start.z+rise*zPerY;
                        float next=terrain.Height(px*30,-pz*30,true);
                        if(next==surface)break;
                        surface=next;
                    }
                }
                fogProjectionNodes[y*FogMeshWidth+x]=new Vector3(
                    (px*30+8192)/64-.5f,(-pz*30+4096)/64-.5f,(Mathf.PerlinNoise(px*.28f,pz*.28f)-.5f)*.1f);
            }
        }
    }
    void PaintFogMask(int shade){
        for(int y=0;y<FogScreenHeight;y++){
            int top=(y/4)*FogMeshWidth;
            float v=(y%4+.5f)*.25f;
            int pixel=y*FogScreenWidth;
            for(int tile=0;tile<FogMeshWidth-1;tile++){
                int node=top+tile;
                var left=fogProjectionNodes[node]+(fogProjectionNodes[node+FogMeshWidth]-fogProjectionNodes[node])*v;
                var right=fogProjectionNodes[node+1]+(fogProjectionNodes[node+FogMeshWidth+1]-fogProjectionNodes[node+1])*v;
                var step=(right-left)*.25f;
                var sample=left+step*.5f;
                for(int dx=0;dx<4;dx++){
                    float visible=SampleFogDisplay(sample.x,sample.y);
                    if(visible>0&&visible<1)visible=Mathf.Clamp01(visible+sample.z*Mathf.Sin(visible*Mathf.PI));
                    screenFogColors[pixel++]=new Color32(0,0,0,(byte)((1-visible)*shade));
                    sample+=step;
                }
            }
        }
    }
    float SampleFogDisplay(float x,float y){
        if(x<=-1||y<=-1||x>=FogGrid.Width||y>=FogGrid.Height)return 0;
        int col=x<0?-1:(int)x,row=y<0?-1:(int)y;
        int i=(row+1)*FogStride+col+1;
        float u=x-col,v=y-row;
        int a=fogDisplay[i],b=fogDisplay[i+1],c=fogDisplay[i+FogStride],d=fogDisplay[i+FogStride+1];
        float top=a+(b-a)*u,bottom=c+(d-c)*u;
        return (top+(bottom-top)*v)*(1f/255f);
    }
    void DrawMinimapFog(Rect rect){if(FogActive&&fogMini)GUI.DrawTextureWithTexCoords(rect,fogMini,new Rect(.125f,.9375f,.75f,-.875f),true);}
    void DisposeFog(){if(fogScreen)Destroy(fogScreen);if(fogMini)Destroy(fogMini);}
}
}
