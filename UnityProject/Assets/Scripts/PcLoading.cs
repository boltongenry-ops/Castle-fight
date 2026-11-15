using System;
using System.IO;
using System.Collections;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    Texture2D loadingBackground,loadingHammer;
    void StartLoadingArt(){loadingBackground=ContentResources.Load<Texture2D>("LoadingBackground");loadingHammer=ContentResources.Load<Texture2D>("LoadingOrcHammer");}
    void DrawLoadingScreen(){
        if(loadingBackground)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),loadingBackground,ScaleMode.ScaleAndCrop);
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var previous=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,Vector3.one*scale);
        var style=new GUIStyle(GUI.skin.label){font=warcraftFont,fontSize=32,normal={textColor=new Color(.91f,.77f,.48f)}};
        GUI.Label(new Rect(44,42,700,64),"CASTLE FIGHT",style);
        GUI.Label(new Rect(835,620,300,50),"Загрузка…",style);
        GUI.matrix=Matrix4x4.identity;
        if(loadingHammer){
            float phase=Mathf.Repeat(Time.realtimeSinceStartup,1.1f)/1.1f;
            float angle=phase<.65f?Mathf.SmoothStep(-12,24,phase/.65f):Mathf.SmoothStep(24,-12,(phase-.65f)/.35f);
            // Rotate at the forearm root outside the physical viewport, so the
            // arm continues through the corner even on a wide phone screen.
            var root=new Vector2(Screen.width+12*scale,Screen.height+12*scale);
            GUIUtility.RotateAroundPivot(angle,root);
            GUI.DrawTexture(new Rect(root.x-232*scale,root.y-232*scale,260*scale,260*scale),loadingHammer,ScaleMode.ScaleToFit);
        }
        GUI.matrix=previous;
    }
    IEnumerator CompleteStartupLoading(){
        // The portable loading window remains responsive while Unity loads the map.
        // Release it only after a video frame or the actual menu can be rendered.
        float until=Time.realtimeSinceStartup+75;
        while(introPlaying&&!introLeaving&&(!introPlayer||introPlayer.frame<1)&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForEndOfFrame();
        string path=BattleLab.Arg("--startup-ready");if(!string.IsNullOrEmpty(path))try{File.WriteAllText(path,"ready");}catch(IOException e){Debug.LogWarning("Startup ready marker: "+e.Message);}
        if(MobileContent.Active)MobileContent.Current.Ready();
        Debug.Log("CF_PC_STARTUP_VISIBLE intro="+introPlaying);
    }
}
}
