using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.ComponentModel;
using System.Runtime.InteropServices;
#endif

namespace CastleFight {
    internal static class NetworkAccountStore {
        internal const string UnityEndpoint="wss://45-15-156-101.fatmetal.net/play-unity-preview";
        internal const string LegacyEndpoint="wss://45-15-156-101.fatmetal.net/play";

        internal static bool SameService(string first,string second) =>
            first==second||(first==UnityEndpoint||first==LegacyEndpoint)&&(second==UnityEndpoint||second==LegacyEndpoint);

        const string Preference="cf-network-account-v1";

        public static JObject Read(){
            string memory=PlayerPrefs.GetString(Preference,"");
            if(memory.Length==0)return null;
            try {
                var clear=Decrypt(Convert.FromBase64String(memory));
                return clear==null?null:JObject.Parse(Encoding.UTF8.GetString(clear));
            }catch(Exception){
                Clear();
                return null;
            }
        }

        public static void Save(string endpoint,string token,string nickname){
            if(string.IsNullOrEmpty(token)||!Uri.TryCreate(endpoint,UriKind.Absolute,out var target)||target.Scheme!="wss")return;
            try{
                var data=new JObject{["endpoint"]=endpoint,["token"]=token,["nickname"]=nickname};
                byte[] encrypted=Encrypt(Encoding.UTF8.GetBytes(data.ToString(Newtonsoft.Json.Formatting.None)));
                if(encrypted!=null){
                    PlayerPrefs.SetString(Preference,Convert.ToBase64String(encrypted));
                    PlayerPrefs.Save();
                }
            }catch(Exception){
                Debug.LogWarning("CF_NETWORK_ACCOUNT_STORE_UNAVAILABLE");
            }
        }

        public static void Clear(){
            PlayerPrefs.DeleteKey(Preference);
            PlayerPrefs.Save();
        }

        static byte[] Encrypt(byte[] clear){
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return TransformWindows(clear,true);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using(var key=AndroidKey(true))
            using(var cipher=AndroidCipher()){
                if(key==null)return null;
                cipher.Call("init",1,key);
                byte[] iv=Unsigned(cipher.Call<sbyte[]>("getIV"));
                byte[] body=Unsigned(cipher.Call<sbyte[]>("doFinal",Signed(clear)));
                if(iv.Length!=12)throw new InvalidOperationException("Invalid account IV");
                var result=new byte[iv.Length+body.Length];
                Buffer.BlockCopy(iv,0,result,0,iv.Length);
                Buffer.BlockCopy(body,0,result,iv.Length,body.Length);
                return result;
            }
#else
            return null;
#endif
        }

        static byte[] Decrypt(byte[] encrypted){
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return TransformWindows(encrypted,false);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if(encrypted.Length<=28)return null;
            var iv=new sbyte[12];
            var body=new sbyte[encrypted.Length-iv.Length];
            Buffer.BlockCopy(encrypted,0,iv,0,iv.Length);
            Buffer.BlockCopy(encrypted,iv.Length,body,0,body.Length);
            using(var key=AndroidKey(false))
            using(var cipher=AndroidCipher())
            using(var parameters=new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec",128,iv)){
                if(key==null)return null;
                cipher.Call("init",2,key,parameters);
                return Unsigned(cipher.Call<sbyte[]>("doFinal",body));
            }
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        struct Blob {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptProtectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);

        [DllImport("crypt32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr block);

        static byte[] TransformWindows(byte[] data,bool protect) {
            const int suppressPrompt=1;
            var input=new Blob{Length=data.Length,Data=Marshal.AllocHGlobal(data.Length)};
            var output=default(Blob);
            try {
                Marshal.Copy(data,0,input.Data,input.Length);
                bool ok=protect
                    ?CryptProtectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,suppressPrompt,out output)
                    :CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,suppressPrompt,out output);
                if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());
                byte[] result=new byte[output.Length];
                Marshal.Copy(output.Data,result,0,result.Length);
                return result;
            }finally{
                Marshal.FreeHGlobal(input.Data);
                if(output.Data!=IntPtr.Zero)LocalFree(output.Data);
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        const string Alias="CastleFightNetworkAccountV1";
        static AndroidJavaObject AndroidKey(bool create){
            using(var type=new AndroidJavaClass("java.security.KeyStore"))
            using(var store=type.CallStatic<AndroidJavaObject>("getInstance","AndroidKeyStore")){
                store.Call("load",new object[]{null,null});
                if(store.Call<bool>("containsAlias",Alias))return store.Call<AndroidJavaObject>("getKey",Alias,null);
            }
            if(!create)return null;
            using(var type=new AndroidJavaClass("javax.crypto.KeyGenerator"))
            using(var generator=type.CallStatic<AndroidJavaObject>("getInstance","AES","AndroidKeyStore"))
            using(var builder=new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder",Alias,3)){
                using(builder.Call<AndroidJavaObject>("setBlockModes",(object)new[]{"GCM"})){}
                using(builder.Call<AndroidJavaObject>("setEncryptionPaddings",(object)new[]{"NoPadding"})){}
                using(var spec=builder.Call<AndroidJavaObject>("build"))generator.Call("init",spec);
                return generator.Call<AndroidJavaObject>("generateKey");
            }
        }
        static AndroidJavaObject AndroidCipher(){
            using(var type=new AndroidJavaClass("javax.crypto.Cipher"))return type.CallStatic<AndroidJavaObject>("getInstance","AES/GCM/NoPadding");
        }
        static sbyte[] Signed(byte[] source){var copy=new sbyte[source.Length];Buffer.BlockCopy(source,0,copy,0,copy.Length);return copy;}
        static byte[] Unsigned(sbyte[] source){var copy=new byte[source.Length];Buffer.BlockCopy(source,0,copy,0,copy.Length);return copy;}
#endif
    }
}
