using System.Collections.Generic;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    Mesh protocolGroundMesh;
    Material protocolGroundMaterial;
    Texture2D protocolFloorTile;
    Color32[] protocolGroundTint;
    float[] protocolGroundDistances;
    float protocolGroundProgress=-1;

    void PrepareProtocolGround(){
        protocolFloorTile=MakeProtocolTile();
        protocolGroundMaterial=new Material(protocolMaterial){mainTexture=protocolFloorTile,renderQueue=2470};
        var ground=new GameObject("Технодром · покрытие арены");
        ground.transform.SetParent(protocolProps.transform,false);
        int firstX=Mathf.Max(0,Mathf.CeilToInt((-6144-terrain.originX)/terrain.tileSize));
        int firstY=Mathf.Max(0,Mathf.CeilToInt((-3584-terrain.originY)/terrain.tileSize));
        int nx=Mathf.Min(terrain.width-1,Mathf.FloorToInt((6144-terrain.originX)/terrain.tileSize))-firstX;
        int ny=Mathf.Min(terrain.height-1,Mathf.FloorToInt((3584-terrain.originY)/terrain.tileSize))-firstY;
        var vertices=new Vector3[(nx+1)*(ny+1)];
        var uv=new Vector2[vertices.Length];
        var triangles=new List<int>(nx*ny*6);
        protocolGroundTint=new Color32[vertices.Length];
        protocolGroundDistances=new float[vertices.Length];
        var reactor=new Vector2(match.ProtocolBeamX+2,0);
        for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){
            int i=y*(nx+1)+x;
            int gridX=x+firstX,gridY=y+firstY;
            var p=new Vector2(terrain.originX+gridX*terrain.tileSize,terrain.originY+gridY*terrain.tileSize)*NativeMapTerrain.Scale;
            float height=terrain.walk[gridY*terrain.width+gridX]*NativeMapTerrain.Scale;
            vertices[i]=new Vector3(p.x,height+.055f,-p.y);
            uv[i]=p/8;
            protocolGroundDistances[i]=(p-reactor).magnitude+Mathf.Sin(p.y*.12f+p.x*.06f)*6;
            if(x==nx||y==ny)continue;
            int next=i+nx+1;
            triangles.Add(i);triangles.Add(i+1);triangles.Add(next+1);
            triangles.Add(i);triangles.Add(next+1);triangles.Add(next);
        }
        protocolGroundMesh=new Mesh{name="Технодром · сетка превращения"};
        protocolGroundMesh.vertices=vertices;protocolGroundMesh.uv=uv;
        protocolGroundMesh.SetTriangles(triangles,0);
        protocolGroundMesh.colors32=protocolGroundTint;
        ground.AddComponent<MeshFilter>().sharedMesh=protocolGroundMesh;
        var renderer=ground.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=protocolGroundMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        protocolGroundProgress=-1;
    }

    void UpdateProtocolGround(float progress){
        if(!protocolGroundMesh||progress==protocolGroundProgress||progress<1&&progress-protocolGroundProgress<.012f)return;
        protocolGroundProgress=progress;
        float radius=Mathf.Lerp(-15,405,progress);
        for(int i=0;i<protocolGroundTint.Length;i++){
            float edge=radius-protocolGroundDistances[i];
            float glow=1-Mathf.Clamp01(Mathf.Abs(edge)/10);
            protocolGroundTint[i]=new Color32((byte)(255-81*glow),255,255,(byte)(255*Mathf.Clamp01((edge+4)/8)));
        }
        protocolGroundMesh.colors32=protocolGroundTint;
    }

    static Texture2D MakeProtocolTile(){
        const int size=128;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            int col=x%64,row=y%64;
            int zone=(x/64+y/64)%2;
            int edge=Mathf.Min(Mathf.Min(col,63-col),Mathf.Min(row,63-row));
            int boltX=Mathf.Min(Mathf.Abs(col-7),Mathf.Abs(col-56));
            int boltY=Mathf.Min(Mathf.Abs(row-7),Mathf.Abs(row-56));
            float paint=Mathf.PerlinNoise(x*.21f,y*.23f)-.5f;
            float baseL=.24f+paint*.16f+(zone==0?.035f:0);
            var steel=new Color(baseL*.92f,baseL*1.13f,baseL*1.28f);
            if(edge<2)steel=new Color(.063f,.088f,.11f);
            else if(edge==2)steel=new Color(.46f,.55f,.58f);
            else if(edge==3)steel=new Color(.18f,.24f,.27f);
            if(boltX*boltX+boltY*boltY<10)steel=row%2==0?new Color(.49f,.5f,.46f):new Color(.17f,.23f,.26f);
            if(zone==0&&row>13&&row<50&&col>=14&&col<=17)
                steel=col==15?new Color(.25f,.75f,.85f):new Color(.08f,.27f,.33f);
            if(zone==1&&col>14&&col<50&&row>23&&row<44){
                steel=row%5<3?new Color(.1f,.14f,.16f):new Color(.35f,.41f,.43f);
                if(col>17&&col<23&&row%5<2)steel=new Color(.56f,.35f,.17f);
            }
            pixels[x+y*size]=steel;
        }
        var tile=new Texture2D(size,size,TextureFormat.RGBA32,true){
            name="Рифлёные плиты технодрома",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2
        };
        tile.SetPixels32(pixels);tile.Apply(true,true);
        return tile;
    }

    void ClearProtocolGround(){
        if(protocolGroundMesh)Destroy(protocolGroundMesh);
        if(protocolGroundMaterial)Destroy(protocolGroundMaterial);
        if(protocolFloorTile)Destroy(protocolFloorTile);
        protocolGroundMesh=null;protocolGroundMaterial=null;protocolFloorTile=null;
        protocolGroundTint=null;protocolGroundDistances=null;
    }
}
}
