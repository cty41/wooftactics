using System.Reflection;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ReadAssemblyName <path-to-assembly.dll>");
    return 1;
}

var name = AssemblyName.GetAssemblyName(args[0]);
Console.WriteLine(name.FullName);
return 0;
