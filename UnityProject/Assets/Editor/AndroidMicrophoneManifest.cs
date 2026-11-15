#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
// Runtime microphone consent belongs to the explicit voice toggle.
public sealed class AndroidMicrophoneManifest : IPostGenerateGradleAndroidProject {
    public int callbackOrder=>100;
    public void OnPostGenerateGradleAndroidProject(string path){
        const string ns="http://schemas.android.com/apk/res/android";
        string file=Path.Combine(path,"src/main/AndroidManifest.xml");var doc=new XmlDocument();doc.Load(file);
        var manifest=doc.DocumentElement;bool record=false;
        foreach(XmlNode node in manifest.SelectNodes("uses-permission"))if(node.Attributes?["name",ns]?.Value=="android.permission.RECORD_AUDIO")record=true;
        if(!record){var p=doc.CreateElement("uses-permission");p.SetAttribute("name",ns,"android.permission.RECORD_AUDIO");manifest.AppendChild(p);}
        var app=manifest.SelectSingleNode("application");XmlElement entry=null;
        foreach(XmlNode node in app.SelectNodes("meta-data"))if(node.Attributes?["name",ns]?.Value=="unityplayer.SkipPermissionsDialog")entry=(XmlElement)node;
        if(entry==null){entry=doc.CreateElement("meta-data");entry.SetAttribute("name",ns,"unityplayer.SkipPermissionsDialog");app.AppendChild(entry);}
        entry.SetAttribute("value",ns,"true");
        bool install=false;foreach(XmlNode node in manifest.SelectNodes("uses-permission"))if(node.Attributes?["name",ns]?.Value=="android.permission.REQUEST_INSTALL_PACKAGES")install=true;
        if(!install){var p=doc.CreateElement("uses-permission");p.SetAttribute("name",ns,"android.permission.REQUEST_INSTALL_PACKAGES");manifest.AppendChild(p);}
        var provider=doc.CreateElement("provider");provider.SetAttribute("name",ns,"com.castlefight.updates.UpdateProvider");provider.SetAttribute("authorities",ns,"${applicationId}.updates");provider.SetAttribute("exported",ns,"false");provider.SetAttribute("grantUriPermissions",ns,"true");app.AppendChild(provider);doc.Save(file);
    }
}
#endif
