using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    FogGrid fog;HumanMatch fogMatch;float fogNext,screenFogNext,fogProjectionNext;Texture2D fogScreen,fogMini;Color32[] screenFogColors,miniFogColors;Vector2[] fogGround,fogCoarse;float[] fogEdge,fogCoarseEdge;Vector3 fogCameraPosition;Quaternion fogCameraRotation;Matrix4x4 fogProjection;float fogCameraFov,fogCameraSize;int fogWidth,fogHeight;double fogProjectionMs,fogMaskMs;
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
        for(int i=0;i<miniFogColors.Length;i++){int display=Math.Max(fog.Current[i],fog.Explored[i]>0?85:0);miniFogColors[i]=new Color32(3,6,9,(byte)((255-display)*.96f));}fogMini.SetPixels32(miniFogColors);fogMini.Apply(false);screenFogNext=0;
    }
    void ApplyFogViews(){
        if(match==null)return;
        foreach(var unit in match.Units)if(units.TryGetValue(unit.id,out var view)&&unit.hp>0)view.gameObject.SetActive(CanSeeUnit(unit)&&(!match.MechMine(unit)||unit.team==0||unit.mechMineAge<2));
        foreach(var hut in match.Huts)if(huts.TryGetValue(hut.id,out var view))view.gameObject.SetActive(!FogActive||hut.team==0||fogKnownHuts.Contains(hut.id));
        bool castleKnown=!FogActive||fog.Known(HumanMatch.CastlePosition(1));castleObjects[1].SetActive(castleKnown&&!options.hdTextures);if(castleViews[1])castleViews[1].gameObject.SetActive(castleKnown&&options.hdTextures);
        if(builders[1])builders[1].gameObject.SetActive(FogVisible(match.Builders[1].position));
        foreach(var shot in match.Shots)if(projectiles.TryGetValue(shot.id,out var view))view.gameObject.SetActive(FogVisible(new Vector2(view.transform.position.x,-view.transform.position.z)));
        if(onlineMatch)foreach(var pair in onlineProjectiles)if(pair.Value)pair.Value.SetActive(ProjectileBorn(pair.Key)&&FogVisible(new Vector2(pair.Value.transform.position.x,-pair.Value.transform.position.z)));
        if(SelectedUnit==null)selectedUnit=-1;if(SelectedHut==null)selectedHut=-1;
    }
    void DrawWorldFog(){
        if(!FogActive||fog==null)return;var camera=Camera.main;if(!camera)return;
        if(!fogScreen){fogScreen=new Texture2D(160,90,TextureFormat.RGBA32,false){name="Terrain projected fog",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};screenFogColors=new Color32[160*90];fogGround=new Vector2[160*90];fogEdge=new float[160*90];fogCoarse=new Vector2[41*24];fogCoarseEdge=new float[41*24];}
        bool moved=fogWidth!=Screen.width||fogHeight!=Screen.height||fogProjection!=camera.projectionMatrix||Vector3.Distance(fogCameraPosition,camera.transform.position)>.02f||Quaternion.Angle(fogCameraRotation,camera.transform.rotation)>.02f||Mathf.Abs(fogCameraFov-camera.fieldOfView)>.01f||Mathf.Abs(fogCameraSize-camera.orthographicSize)>.01f;
        if(Event.current.type==EventType.Repaint&&(Time.unscaledTime>=screenFogNext||moved&&Time.unscaledTime>=fogProjectionNext)){
            var watch=System.Diagnostics.Stopwatch.StartNew();screenFogNext=Time.unscaledTime+.18f;
            if(moved){fogProjectionNext=Time.unscaledTime+1f/12;fogProjection=camera.projectionMatrix;fogCameraPosition=camera.transform.position;fogCameraRotation=camera.transform.rotation;fogCameraFov=camera.fieldOfView;fogCameraSize=camera.orthographicSize;fogWidth=Screen.width;fogHeight=Screen.height;
            for(int y=0;y<24;y++)for(int x=0;x<41;x++){
                var ray=camera.ScreenPointToRay(new Vector3(x*4f/160*Screen.width,y*4f/90*Screen.height));float height=12;var point=ray.origin;
                for(int i=0;i<6;i++){float travel=ray.direction.y<-.0001f?Mathf.Max(0,(height-ray.origin.y)/ray.direction.y):0;point=ray.origin+ray.direction*travel;height=terrain.Height(point.x*30,-point.z*30,true);}
                int index=y*41+x;fogCoarse[index]=new Vector2(point.x,-point.z);fogCoarseEdge[index]=(Mathf.PerlinNoise(point.x*.28f,point.z*.28f)-.5f)*.1f;
            }
            for(int y=0;y<90;y++)for(int x=0;x<160;x++){float u=(x+.5f)/4,v=(y+.5f)/4;int cx=(int)u,cy=(int)v,index=cy*41+cx,target=y*160+x;u-=cx;v-=cy;fogGround[target]=Vector2.Lerp(Vector2.Lerp(fogCoarse[index],fogCoarse[index+1],u),Vector2.Lerp(fogCoarse[index+41],fogCoarse[index+42],u),v);fogEdge[target]=Mathf.Lerp(Mathf.Lerp(fogCoarseEdge[index],fogCoarseEdge[index+1],u),Mathf.Lerp(fogCoarseEdge[index+41],fogCoarseEdge[index+42],u),v);}
            fogProjectionMs=watch.Elapsed.TotalMilliseconds;}
            watch.Restart();for(int i=0;i<screenFogColors.Length;i++){float visible=fog.Sample(fogGround[i]);float edge=visible>0&&visible<1?fogEdge[i]*Mathf.Sin(visible*Mathf.PI):0;screenFogColors[i]=new Color32(2,3,5,(byte)(255*(1-Mathf.Clamp01(visible+edge))));}
            fogScreen.SetPixels32(screenFogColors);fogScreen.Apply(false);
            fogMaskMs=watch.Elapsed.TotalMilliseconds;
        }
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),fogScreen,ScaleMode.StretchToFill,true);
    }
    void DrawMinimapFog(Rect rect){if(FogActive&&fogMini)GUI.DrawTextureWithTexCoords(rect,fogMini,new Rect(.125f,.9375f,.75f,-.875f),true);}
    void DisposeFog(){if(fogScreen)Destroy(fogScreen);if(fogMini)Destroy(fogMini);}
}
}
