using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
class CompareManaged {
 static IEnumerable<TypeDefinition> Walk(TypeDefinition t){yield return t;foreach(var child in t.NestedTypes)foreach(var nested in Walk(child))yield return nested;}
 static bool UserType(TypeDefinition t){return !t.FullName.Contains("<")&&!t.CustomAttributes.Any(a=>a.AttributeType.Name=="CompilerGeneratedAttribute");}
 static string Layout(TypeDefinition t,IEnumerable<FieldDefinition> fields){return (t.BaseType==null?"":t.BaseType.FullName)+"|"+string.Join(";",fields.Select(f=>f.Name+":"+f.FieldType.FullName+":"+f.IsStatic));}
 static readonly HashSet<string> RaceFogRuntimeFields=new HashSet<string>{
  "fogProjectionNext","fogGround","fogCoarse","fogEdge","fogCoarseEdge","fogCameraFov","fogCameraSize",
  "FogScreenWidth","FogScreenHeight","FogMeshWidth","FogMeshHeight","FogStride","fogDisplay","fogProjectionNodes",
  "fogViewport","fogSoft","desktopMenuBuilders","raceAssetSweep","DesktopRaceCatalogs"
 };
 static readonly HashSet<string> WorkshopRuntimeFields=new HashSet<string>{
  "workshops038","WorkshopCategories","WorkshopShelf","WorkshopLook","WorkshopGold","WorkshopIvory","WorkshopText"
 };
 static IEnumerable<FieldDefinition> VerifyPrivateRuntimeFields(TypeDefinition type,HashSet<string> names,bool staticOnly){
  foreach(var field in type.Fields){
   if(!names.Contains(field.Name)){yield return field;continue;}
   if(!field.IsPrivate||(staticOnly&&!field.IsStatic)||field.CustomAttributes.Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeField"||a.AttributeType.FullName=="UnityEngine.SerializeReference"))
    throw new Exception("Runtime field failed serialization check: "+field.FullName);
  }
 }
 static int Main(string[] args){
  var a=AssemblyDefinition.ReadAssembly(args[0]);var b=AssemblyDefinition.ReadAssembly(args[1]);int types=0;
  foreach(var t in a.MainModule.Types.SelectMany(Walk).Where(UserType)){
   var n=b.MainModule.Types.SelectMany(Walk).Single(x=>x.FullName==t.FullName);
   bool raceFog=t.FullName=="CastleFight.PcGame"&&args.Contains("--verify-race-fog-private-fields");
   bool workshop=t.FullName=="CastleFight.PcGame"&&args.Contains("--verify-workshop-static-fields");
   var ignored=raceFog?RaceFogRuntimeFields:workshop?WorkshopRuntimeFields:null;
   var oldFields=ignored!=null?VerifyPrivateRuntimeFields(t,ignored,workshop):t.Fields;
   var newFields=ignored!=null?VerifyPrivateRuntimeFields(n,ignored,workshop):n.Fields;
   if(Layout(t,oldFields)!=Layout(n,newFields))throw new Exception("Changed field layout: "+t.FullName);
   if(raceFog)Console.WriteLine("RACE_FOG_PRIVATE_FIELDS_PASS serializedLayout=unchanged");
   if(workshop)Console.WriteLine("WORKSHOP_STATIC_FIELDS_PASS serializedLayout=unchanged");
   types++;
  }
  foreach(var r in b.MainModule.AssemblyReferences)if(!a.MainModule.AssemblyReferences.Any(x=>x.FullName==r.FullName)){
   if(args.Length<3||!(r.Name=="Unity.Burst"||r.Name=="Unity.Collections"||r.Name=="UnityEngine.BurstModule"))throw new Exception("New dependency: "+r.FullName);
   var shipped=AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(args[2],r.Name+".dll"));if(shipped.Name.FullName!=r.FullName)throw new Exception("Burst dependency differs from verified baseline: "+r.FullName);
  }
  var m=b.MainModule.Types.Single(t=>t.FullName=="CastleFight.HumanMatch").Methods.Single(x=>x.Name=="ApplyEffectData");
  if(!m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld&&((FieldReference)i.Operand).Name=="dotTick"))throw new Exception("Poison timer fix missing");
  var tower=b.MainModule.Types.Single(t=>t.FullName=="CastleFight.HumanMatch").Methods.Single(x=>x.Name=="NerubAfterWeapon");
  foreach(string literal in new[]{"hNT1","hNT2","poison"})if(!tower.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand==literal))throw new Exception("Tower poison branch missing: "+literal);
  if(!tower.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.Name=="ApplyEffect"))throw new Exception("Tower poison application missing");
  if(!b.MainModule.Types.Single(t=>t.FullName=="CastleFight.ContentCatalog031").Methods.Any(x=>x.Name=="AssetUrl"))throw new Exception("Per-package Android content URL support missing");
  Console.WriteLine("TOWER_POISON_IL_PASS base=present upgrade=present application=present contentUrl=present");
  Console.WriteLine("MANAGED_COMPATIBILITY_PASS userTypes="+types+" fieldLayouts=unchanged dependencies=verifiedAgainstBaseline poisonFix=present");return 0;
 }
}
