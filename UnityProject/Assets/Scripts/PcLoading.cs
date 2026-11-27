using System.IO;
using System.Collections;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    Texture2D loadingBackground,loadingSiege;
    float? loadingPreviewTime;
    void StartLoadingArt(){
        if(Application.isMobilePlatform){ContentLoadingScreen.LoadEmbeddedArt();return;}
        var art=Path.Combine(Application.streamingAssetsPath,"Frontend");
        loadingBackground=ReadCampaignPainting(art,"CampaignSiegeBackdrop");
        loadingSiege=ReadCampaignTexture(Path.Combine(art,"CampaignCastleBody.png"));
    }
    void DrawLoadingScreen()=>ContentLoadingScreen.Draw(loadingBackground,loadingSiege,warcraftFont,
        "Готовим карту и модели…",-1,"",false,startup:true);

    void DisposeLoadingArt(){
        if(loadingBackground)Destroy(loadingBackground);
        if(loadingSiege)Destroy(loadingSiege);
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
