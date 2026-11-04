using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
static class Program { static int Main(string[] args) { int count=0; foreach(string path in args){ var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(path),new CSharpParseOptions(LanguageVersion.CSharp9),path); var errors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();foreach(var error in errors)Console.Error.WriteLine(error);if(errors.Length>0)return 1;count++;}Console.WriteLine("PASS: "+count+" authored Unity C# files parse as C#9. Syntax ONLY; NO Unity API/type/shader/Play compilation claim.");return 0;} }
