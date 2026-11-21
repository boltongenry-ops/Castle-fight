using Wc3ModelViewer.Core.Formats;
using System.Text.Json;
foreach(var file in Directory.GetFiles(args[0],"*.mdx")){
 var model=MdxReader.Read(File.ReadAllBytes(file));
 var lod=model.Geosets.Select(g=>g.LodId).OrderBy(i=>Math.Abs(i-2)).DefaultIfEmpty(-1).First();
 var parts=new List<object>();
 foreach(var g in model.Geosets.Where(g=>g.LodId==lod && g.VertexCount>0 && g.Indices.Length>0)){
  var layer=model.Materials[g.MaterialId].Layers.First();
  var path=model.Textures[layer.DiffuseTextureId].FileName;
  if(string.IsNullOrEmpty(path))continue;
  parts.Add(new {texture=path,points=g.Positions.Select(p=>new[]{-p.X/30,p.Z/30,-p.Y/30}), normals=g.Normals.Select(n=>new[]{-n.X,n.Z,-n.Y}),uv=g.UvLayers[0].Select(v=>new[]{v.X,1-v.Y}),indices=g.Indices});
 }
 File.WriteAllText(Path.ChangeExtension(file,".json"),JsonSerializer.Serialize(parts));
}

