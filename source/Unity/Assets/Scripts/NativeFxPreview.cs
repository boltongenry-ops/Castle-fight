using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
// Native generic_fire emitter proof of concept. No ribbons, MDX global tracks or tails yet.
// Kept outside gameplay: this preview never changes damage, target selection or timing.
public sealed class NativeFxPreview : MonoBehaviour {
    public Material particleMaterial;
    readonly List<Transform> missiles=new List<Transform>();
    public int count=24;
    void Start() {
        var emitters=JArray.Parse(ContentResources.Load<TextAsset>("native-fx").text);
        for(int i=0;i<count;i++) {
            var root=new GameObject("Native fire preview "+i).transform;root.SetParent(transform,false);missiles.Add(root);
            foreach(JObject e in emitters) {
                var go=new GameObject("WC3 ParticleEmitter2");go.transform.SetParent(root,false);
                var p=(JArray)e["position"];go.transform.localPosition=new Vector3((float)p[0],(float)p[2],(float)p[1])/30;
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.playOnAwake=false;main.startLifetime=(float)e["lifetime"];
                main.startSpeed=(float)e["speed"]/30;main.startSize=1;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.maxParticles=Mathf.Clamp(Mathf.RoundToInt((float)e["rate"]*(float)e["lifetime"]),1,128);
                main.gravityModifier=(float)e["gravity"]/(30*9.81f);main.useUnscaledTime=false;
                var emission=ps.emission;emission.rateOverTime=(float)e["rate"];
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=Mathf.Min(90,(float)e["latitude"]*.5f);shape.radius=.05f;
                var color=ps.colorOverLifetime;color.enabled=true;
                var colors=(JArray)e["colors"];var alpha=(JArray)e["alphas"];float mid=(float)e["middle"];
                var gradient=new Gradient();var ck=new GradientColorKey[3];var ak=new GradientAlphaKey[3];
                var scales=(JArray)e["scales"];var curve=new AnimationCurve();
                for(int k=0;k<3;k++) {float t=k==0?0:k==1?mid:1;var c=(JArray)colors[k];ck[k]=new GradientColorKey(new Color((float)c[0],(float)c[1],(float)c[2]),t);ak[k]=new GradientAlphaKey((float)alpha[k]/255,t);curve.AddKey(t,(float)scales[k]/30);}
                gradient.SetKeys(ck,ak);color.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,curve);
                var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=(int)e["columns"];sheet.numTilesY=(int)e["rows"];sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
                var renderer=ps.GetComponent<ParticleSystemRenderer>();
                var mat=new Material(particleMaterial);mat.mainTexture=ContentResources.Load<Texture2D>((string)e["resource"]);renderer.sharedMaterial=mat;
                ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(1234+i);ps.Play();
            }
        }
    }
    void Update() {
        for(int i=0;i<missiles.Count;i++)missiles[i].position=new Vector3(Mathf.Repeat(Time.time*14+i*4,60)-30,12,(i%8-4)*4);
    }
}
}
