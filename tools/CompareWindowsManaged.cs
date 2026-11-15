using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
class CompareManaged {
 static IEnumerable<TypeDefinition> Walk(TypeDefinition t){yield return t;foreach(var child in t.NestedTypes)foreach(var nested in Walk(child))yield return nested;}
 static bool UserType(TypeDefinition t){return !t.FullName.Contains("<")&&!t.CustomAttributes.Any(a=>a.AttributeType.Name=="CompilerGeneratedAttribute");}
 static string Layout(TypeDefinition t){return (t.BaseType==null?"":t.BaseType.FullName)+"|"+string.Join(";",t.Fields.Select(f=>f.Name+":"+f.FieldType.FullName+":"+f.IsStatic));}
 static int Main(string[] args){
  var a=AssemblyDefinition.ReadAssembly(args[0]);var b=AssemblyDefinition.ReadAssembly(args[1]);int types=0;
  foreach(var t in a.MainModule.Types.SelectMany(Walk).Where(UserType)){
   var n=b.MainModule.Types.SelectMany(Walk).Single(x=>x.FullName==t.FullName);if(Layout(t)!=Layout(n))throw new Exception("Changed field layout: "+t.FullName);types++;
  }
  foreach(var r in b.MainModule.AssemblyReferences)if(!a.MainModule.AssemblyReferences.Any(x=>x.FullName==r.FullName))throw new Exception("New dependency: "+r.FullName);
  var m=b.MainModule.Types.Single(t=>t.FullName=="CastleFight.HumanMatch").Methods.Single(x=>x.Name=="ApplyEffectData");
  if(!m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld&&((FieldReference)i.Operand).Name=="dotTick"))throw new Exception("Poison timer fix missing");
  Console.WriteLine("MANAGED_COMPATIBILITY_PASS userTypes="+types+" fieldLayouts=unchanged dependencies=unchanged poisonFix=present");return 0;
 }
}
