using Mono.Cecil;
using var module = ModuleDefinition.ReadModule(args[0]);
IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(All(t.NestedTypes)));
foreach (var type in All(module.Types).Where(t => args.Skip(1).Contains(t.Name)))
{
    Console.WriteLine(type.FullName);
    foreach (var field in type.Fields) Console.WriteLine($"  field {field.FieldType} {field.Name} {field.Constant}");
    foreach (var property in type.Properties) Console.WriteLine($"  property {property.PropertyType} {property.Name}");
    foreach (var method in type.Methods.Where(m => !m.IsGetter && !m.IsSetter)) Console.WriteLine($"  method {method}");
}
