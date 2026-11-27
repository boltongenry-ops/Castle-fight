using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    ReforgedMap MapArt=>ReforgedMap.For(this);
    void DrawMapArtSettings(){
        var art=MapArt;
        AtlasSurface(new Rect(240,66,800,588),true);
        Text(286,93,480,50,"Оформление карты",atlasTitle);
        bool interactive=GUI.enabled;GUI.enabled=interactive&&!art.Busy;
        if(Button("map.models",new Rect(777,96,217,43),"Модели бойцов")){art.Panel=false;art.Confirm=false;}
        Accent(286,153,708);
        if(AtlasEdition("map.classic",new Rect(286,173,345,205),"Классическая",0,!art.Draft))art.Draft=false;
        if(AtlasEdition("map.reforged",new Rect(649,173,345,205),"Reforged",1,art.Draft))art.Draft=true;
        Text(286,388,708,28,"Текстуры земли, дорожки и декорации. Карта скачивается отдельно от моделей.",small);
        Text(286,417,710,54,art.Confirm?"Удалить скачанное оформление карты?":art.Status!=""?art.Status:art.Installed?"Пакет карты скачан":"Пакет карты ещё не скачан",small);
        if(art.Busy){
            var bar=new Rect(286,486,708,12);Fill(bar,new Color(.12f,.14f,.14f));
            Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(art.Progress),bar.height),new Color(.71f,.54f,.27f));
            GUI.enabled=interactive;
            if(art.Status=="Загрузка карты…"&&Button("map.stop",new Rect(644,515,350,48),"Остановить"))art.Abort();
        }else if(art.Confirm){
            if(Button("map.keep",new Rect(286,515,330,48),"Оставить"))art.Confirm=false;
            if(Button("map.remove",new Rect(644,515,350,48),"Удалить пакет"))art.Remove();
        }else if(art.Installed){
            if(Button("map.delete",new Rect(644,515,350,48),"Удалить карту Reforged"))art.Confirm=true;
        }else if(art.Draft&&Button("map.download",new Rect(644,515,350,48),"Скачать карту Reforged",true))StartCoroutine(art.Download(controls));
        GUI.enabled=interactive&&!art.Busy;
        if(Button("map.back",new Rect(286,586,260,47),"← К настройкам")){art.Panel=false;HdModels.Panel=false;art.Confirm=false;}
        if(art.Draft!=art.Preferred&&!art.Busy)
            Text(575,586,417,47,"Новый стиль включится после «Применить» в настройках.",small);
        GUI.enabled=interactive;
    }
}
}
