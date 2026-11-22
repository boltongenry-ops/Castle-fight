using UnityEngine;

namespace CastleFight {
public sealed partial class PepeSkillFx {
    static readonly Color Obsidian=new Color(.22f,.24f,.32f);
    static readonly Color IronWhite=new Color(.79f,.86f,.98f);
    static readonly Color Blood=new Color(.84f,.12f,.18f);
    static readonly Color Curse=new Color(.66f,.27f,.95f);
    static readonly Color Venom=new Color(.47f,.92f,.19f);
    static readonly Color Ember=new Color(1f,.37f,.075f);

    bool PaintBlackHand(string id) {
        switch(id) {
            case "blackhand:guard":
            case "blackhand:bash":
                float shield=.43f+age*.31f;
                for(int i=0;i<3;i++){
                    Vector3 face=Rad(phase+2.1f*i);
                    Petal(face*shield+Vector3.up*.37f,face*shield+Vector3.up*1.02f,
                        Vector3.Cross(face,Vector3.up)*.22f,Obsidian,IronWhite);
                }
                Wave(.23f+.72f*age,.09f,.085f,Gold,phase,4.9f);
                return true;
            case "blackhand:pike":
            case "blackhand:bolt":
                Fragments(4,IronWhite,Gold,.46f);
                return true;
            case "blackhand:bleed":
                Fragments(5,Blood,new Color(1f,.40f,.29f),.46f);
                return true;
            case "blackhand:execute":
                Vector3 up=Vector3.up*(1.68f-1.22f*Mathf.Clamp01(age*1.85f));
                Petal(up-Rad(phase)*.58f,up+Rad(phase)*.82f,Vector3.up*.25f,IronWhite,Gold);
                Wave(.27f+age*.72f,.08f,.08f,Blood,phase,4.25f);
                return true;
            case "blackhand:invoke":
            case "blackhand:tithe":
            case "blackhand:tithe-hit":
                Wave(.55f+.08f*Mathf.Sin(age*6.28f),.23f,.09f,Curse,phase+age*2,5.9f);
                Wave(.39f+age*.25f,.73f,.065f,Gold,-phase-age*2,5.8f);
                Fragments(id=="blackhand:tithe-hit"?6:3,Obsidian,Curse,.6f);
                return true;
            case "blackhand:bell":
            case "blackhand:silence":
                for(int echo=0;echo<3;echo++)
                    Wave(.19f+.75f*Mathf.Clamp01(age*1.35f-echo*.16f),.10f+echo*.18f,.075f,
                        echo==1?Gold:IronWhite,phase,5.5f);
                return true;
            case "blackhand:heal":
                Wave(.36f+.60f*age,.08f,.11f,Pearl);
                Fragments(7,Gold,Mint,1.02f);
                return true;
            case "blackhand:judgment":
                Wave(.92f,.06f,.07f,Curse);
                if(age<.2f)Wave(Mathf.Clamp01(age*5),.12f,.09f,Gold);
                var foot=Vector3.up*.15f;var mast=Vector3.up*1.30f;var side=Rad(phase);
                Petal(foot,mast,side*.035f,IronWhite,Gold);
                Petal(mast,mast+side*.45f-Vector3.up*.42f,Vector3.up*.14f,Obsidian,Curse);
                return true;
            case "blackhand:raven":
                Wave(.43f+age*.44f,.065f,.072f,Curse);
                Fragments(4,Obsidian,IronWhite,.65f);
                return true;
            case "blackhand:poison":
                Wave(.31f+age*.64f,.04f,.09f,Venom);
                Fragments(9,Leaf,Venom,.29f);
                return true;
            case "blackhand:fire":
                Wave(.28f+age*.68f,.035f,.10f,Ember);
                Fragments(11,Ember,Gold,.42f);
                return true;
            case "blackhand:curse":
                Wave(.79f+age*.19f,.04f,.07f,Curse,phase+age,5.65f);
                Fragments(8,Obsidian,Curse,.34f);
                return true;
            case "blackhand:stone":
                Wave(.22f+age*.77f,.04f,.062f,IronWhite);
                Fragments(8,Bronze,IronWhite,.32f);
                return true;
            default:return false;
        }
    }

    public void BlackHandStatus(Vector3 feet,string id,float time,int seed) {
        if(!mesh)return;
        origin=transform.InverseTransformPoint(feet+Vector3.up*.12f);
        radius=1.15f;
        opacity=.57f;
        float phase=time*1.7f+seed*.37f;
        Color color=id=="BHPs"?Venom:id=="BHW1"||id=="BHW2"?Blood:Curse;
        Wave(.69f,.055f,.06f,color,phase,1.45f);
        Wave(.69f,.055f,.06f,color,phase+Mathf.PI,1.45f);
    }

    void Fragments(int count,Color shade,Color highlight,float height) {
        for(int i=0;i<count;i++) {
            float angle=phase+2*Mathf.PI*i/count;
            Vector3 ray=Rad(angle),side=Rad(angle+Mathf.PI*.5f);
            float fall=Mathf.Sin(age*Mathf.PI);
            Vector3 end=ray*(.30f+age*.63f)+Vector3.up*(.12f+height*fall);
            Petal(end-ray*.15f, end+Vector3.up*.12f,side*.056f,shade,highlight);
        }
    }
}
}
