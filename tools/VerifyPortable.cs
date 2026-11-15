using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
class VerifyPortable {
 static int Main(string[] args){
  using(var file=File.OpenRead(args[0]))using(var reader=new BinaryReader(file)){
   file.Position=file.Length-64;long start=reader.ReadInt64(),size=reader.ReadInt64();var expected=reader.ReadBytes(32);
   if(Encoding.ASCII.GetString(reader.ReadBytes(16))!="CFPORTABLE-V1!!!"||start+size+64!=file.Length)throw new Exception("Portable footer invalid");
   file.Position=start;
   using(var payload=new MemoryStream()){
    var buffer=new byte[1024*1024];long left=size;
    while(left>0){int n=file.Read(buffer,0,(int)Math.Min(buffer.Length,left));if(n<=0)throw new EndOfStreamException();payload.Write(buffer,0,n);left-=n;}
    payload.Position=0;using(var sha=SHA256.Create())if(!sha.ComputeHash(payload).SequenceEqual(expected))throw new Exception("Portable payload checksum mismatch");
    payload.Position=0;int count=0;long bytes=0;
    using(var zip=new ZipArchive(payload,ZipArchiveMode.Read))foreach(var entry in zip.Entries){
     if(entry.Name.Length==0)continue;
     using(var source=entry.Open())using(var sha=SHA256.Create()){var hash=sha.ComputeHash(source);if(hash.Length!=32)throw new Exception("Hash failure");}
     bytes+=entry.Length;count++;
    }
    Console.WriteLine("PORTABLE_DOTNET_PASS files="+count+" extractedBytes="+bytes+" payloadBytes="+size);
   }
  }
  return 0;
 }
}
