using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TypeGen.Core.Generator;
using TypeGen.Core.Converters;
using Catan3.Shared.TypeScript;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

// Determine output path relative to this runner's location
// Runner is in: Catan3.Shared/TypeScript/TypeGenRunner/
// Output should be: react-ui/types/generated/models/
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
var outputPath = Path.Combine(repoRoot, "react-ui", "types", "generated", "models");

Console.WriteLine("TypeGen Runner - Generating TypeScript types");
Console.WriteLine($"Output path: {outputPath}");
Console.WriteLine();

// Ensure output directory exists
Directory.CreateDirectory(outputPath);

// Step 1: Run TypeGen to generate base types
Console.WriteLine("Step 1: Running TypeGen...");
var options = new GeneratorOptions
{
    BaseOutputDirectory = outputPath,
    CreateIndexFile = true,
    SingleQuotes = true,
    EnumStringInitializers = true,
    PropertyNameConverters = [new PascalCaseToCamelCaseConverter()],
    TypeNameConverters = []
};

var generator = new Generator(options);
var spec = new CatanTypeGenSpec();

var generatedFiles = await generator.GenerateAsync([spec]);
var fileList = generatedFiles.ToList();

Console.WriteLine($"  Generated {fileList.Count} TypeScript model files");

// Step 2: Post-process to remove MVVM artifacts
Console.WriteLine();
Console.WriteLine("Step 2: Post-processing (removing MVVM artifacts)...");
PostProcessGeneratedFiles(outputPath);

// Step 2b: Remove properties marked with [JsonIgnore] in C#
Console.WriteLine();
Console.WriteLine("Step 2b: Removing [JsonIgnore] properties from generated types...");
RemoveJsonIgnoredProperties(outputPath);

// Step 3: Generate enum descriptions
Console.WriteLine();
Console.WriteLine("Step 3: Generating enum descriptions...");
GenerateEnumDescriptions(outputPath);

Console.WriteLine();
Console.WriteLine("TypeGen generation complete!");

// ============================================================================
// Post-Processing Functions
// ============================================================================

/// <summary>
/// Post-processes generated TypeScript files to remove .NET MVVM artifacts.
/// </summary>
static void PostProcessGeneratedFiles(string outputPath)
{
    // Files to delete (MVVM artifacts with no TypeScript equivalent)
    var filesToDelete = new[]
    {
        "observable-object.ts",
        "i-notify-property-changed.ts",
        "i-notify-property-changing.ts"
    };

    // Types to remove from imports and extends clauses
    var typesToRemove = new[]
    {
        "ObservableObject",
        "INotifyPropertyChanged",
        "INotifyPropertyChanging"
    };

    // Delete MVVM files
    foreach (var file in filesToDelete)
    {
        var filePath = Path.Combine(outputPath, file);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Console.WriteLine($"  Deleted {file}");
        }
    }

    // Process all remaining .ts files to clean imports and extends
    var tsFiles = Directory.GetFiles(outputPath, "*.ts");
    foreach (var filePath in tsFiles)
    {
        var fileName = Path.GetFileName(filePath);
        var content = File.ReadAllText(filePath);
        var originalContent = content;

        // Remove imports for deleted types
        foreach (var type in typesToRemove)
        {
            var kebabName = ToKebabCase(type);
            // Match: import { TypeName } from './kebab-name';
            var importPattern = $@"import \{{ {type} \}} from '\.\/{kebabName}';\r?\n";
            content = Regex.Replace(content, importPattern, "");
        }

        // Remove extends clauses for deleted types
        // Match: extends INotifyPropertyChanged, INotifyPropertyChanging
        // or: extends ObservableObject
        // or: extends SomeType, INotifyPropertyChanged
        foreach (var type in typesToRemove)
        {
            // Remove ", TypeName" or "TypeName, " from extends
            content = Regex.Replace(content, $@",\s*{type}", "");
            content = Regex.Replace(content, $@"{type},\s*", "");
            // Remove "extends TypeName " when it's the only base
            content = Regex.Replace(content, $@"\s+extends\s+{type}\s+\{{", " {");
        }

        // Convert classes to interfaces (avoid TypeScript strict mode issues with uninitialized properties)
        // Match: export class ClassName {
        // Replace with: export interface ClassName {
        content = Regex.Replace(content, @"export\s+class\s+(\w+)\s*\{", "export interface $1 {");

        // Remove default values from interface properties (e.g., "seed: number = 287818514;" -> "seed: number;")
        content = Regex.Replace(content, @"(\w+:\s*\w+)\s*=\s*[^;]+;", "$1;");

        // Clean up empty extends clauses (extends  {)
        content = Regex.Replace(content, @"\s+extends\s+\{", " {");

        // Update index.ts to remove exports for deleted files
        if (fileName == "index.ts")
        {
            foreach (var file in filesToDelete)
            {
                var moduleName = Path.GetFileNameWithoutExtension(file);
                var exportPattern = $@"export \* from '\.\/{moduleName}';\r?\n";
                content = Regex.Replace(content, exportPattern, "");
            }
        }

        // Write back if changed
        if (content != originalContent)
        {
            File.WriteAllText(filePath, content);
            Console.WriteLine($"  Cleaned {fileName}");
        }
    }
}

// ============================================================================
// Enum Description Generation
// ============================================================================

/// <summary>
/// Generates a TypeScript file containing enum description mappings
/// extracted from C# [Description] attributes.
/// </summary>
static void GenerateEnumDescriptions(string outputPath)
{
    var sb = new StringBuilder();

    sb.AppendLine("/**");
    sb.AppendLine(" * This is a TypeGen auto-generated file.");
    sb.AppendLine(" * Contains enum description mappings extracted from C# [Description] attributes.");
    sb.AppendLine(" * Any changes made to this file can be lost when this file is regenerated.");
    sb.AppendLine(" */");
    sb.AppendLine();

    // List of enums that have descriptions
    var enumsWithDescriptions = new (Type EnumType, string TypeScriptName)[]
    {
        (typeof(GameState), "GameState"),
        (typeof(Entitlement), "Entitlement"),
        (typeof(ActionType), "ActionType"),
    };

    // Generate imports
    foreach (var (_, tsName) in enumsWithDescriptions)
    {
        var fileName = ToKebabCase(tsName);
        sb.AppendLine($"import {{ {tsName} }} from './{fileName}';");
    }
    sb.AppendLine();

    // Generate description records for each enum
    foreach (var (enumType, tsName) in enumsWithDescriptions)
    {
        sb.AppendLine("/**");
        sb.AppendLine($" * Display text for {tsName} enum values.");
        sb.AppendLine(" * Maps each enum value to its user-friendly description from C# [Description] attributes.");
        sb.AppendLine(" */");
        sb.AppendLine($"export const {tsName}Descriptions: Record<{tsName}, string> = {{");

        var values = Enum.GetValues(enumType);
        var lastIndex = values.Length - 1;
        var index = 0;

        foreach (var value in values)
        {
            var name = value.ToString()!;
            var description = GetEnumDescription(enumType, value);
            var comma = index < lastIndex ? "," : "";
            sb.AppendLine($"  [{tsName}.{name}]: '{EscapeString(description)}'{comma}");
            index++;
        }

        sb.AppendLine("};");
        sb.AppendLine();
    }

    // Generate helper function
    sb.AppendLine("/**");
    sb.AppendLine(" * Gets the display description for an enum value.");
    sb.AppendLine(" *");
    sb.AppendLine(" * @param descriptions - The description record for the enum type");
    sb.AppendLine(" * @param value - The enum value to look up");
    sb.AppendLine(" * @returns The description string, or the value itself if no description found");
    sb.AppendLine(" */");
    sb.AppendLine("export function getEnumDescription<T extends string>(");
    sb.AppendLine("  descriptions: Record<T, string>,");
    sb.AppendLine("  value: T");
    sb.AppendLine("): string {");
    sb.AppendLine("  return descriptions[value] ?? value;");
    sb.AppendLine("}");

    var filePath = Path.Combine(outputPath, "enum-descriptions.ts");
    File.WriteAllText(filePath, sb.ToString());
    Console.WriteLine($"  Generated enum-descriptions.ts");

    // Add export to index.ts
    var indexPath = Path.Combine(outputPath, "index.ts");
    if (File.Exists(indexPath))
    {
        var indexContent = File.ReadAllText(indexPath);
        if (!indexContent.Contains("enum-descriptions"))
        {
            indexContent += "export * from './enum-descriptions';\n";
            File.WriteAllText(indexPath, indexContent);
            Console.WriteLine("  Updated index.ts with enum-descriptions export");
        }
    }
}

/// <summary>
/// Gets the [Description] attribute value for an enum value, or the enum name if no description.
/// </summary>
static string GetEnumDescription(Type enumType, object value)
{
    var name = value.ToString()!;
    var field = enumType.GetField(name);

    if (field == null)
        return name;

    var attr = field.GetCustomAttribute<DescriptionAttribute>();
    return attr?.Description ?? name;
}

// ============================================================================
// JsonIgnore Property Removal
// ============================================================================

/// <summary>
/// Removes properties from generated TypeScript interfaces that are marked with [JsonIgnore] in C#.
/// This ensures the TypeScript types match what actually gets serialized to JSON.
/// </summary>
static void RemoveJsonIgnoredProperties(string outputPath)
{
    // Map of C# type -> list of property names that have [JsonIgnore]
    // We build this by reflecting over the actual C# types
    var jsonIgnoredProperties = GetJsonIgnoredPropertiesMap();

    foreach (var (typeName, ignoredProps) in jsonIgnoredProperties)
    {
        if (ignoredProps.Count == 0) continue;

        var fileName = ToKebabCase(typeName) + ".ts";
        var filePath = Path.Combine(outputPath, fileName);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"  Warning: {fileName} not found, skipping");
            continue;
        }

        var content = File.ReadAllText(filePath);
        var originalContent = content;

        // Remove each ignored property line
        foreach (var propName in ignoredProps)
        {
            // TypeGen converts PascalCase to camelCase for property names
            var camelPropName = char.ToLowerInvariant(propName[0]) + propName[1..];

            // Match property declaration lines. Handle:
            // - Simple types: "    propertyName: SomeType;"
            // - Complex types: "    propertyName: { [key in Direction]?: HexCoordinates; };"
            // - Lines with trailing comments: "    propertyName: SomeType; // comment"
            // - Lines with trailing whitespace
            // Use greedy .* after semicolon to capture any trailing content before newline
            var pattern = $@"^\s*{camelPropName}:.*?;.*\r?\n";
            content = Regex.Replace(content, pattern, "", RegexOptions.Multiline);
        }

        // Remove any imports that are no longer needed
        content = RemoveUnusedImports(content);

        if (content != originalContent)
        {
            File.WriteAllText(filePath, content);
            Console.WriteLine($"  Fixed {fileName}: removed {ignoredProps.Count} [JsonIgnore] properties");
        }
    }
}

/// <summary>
/// Builds a map of C# type names to property names that should be excluded from TypeScript.
/// This includes properties marked with [JsonIgnore] and static properties (which are never serialized).
/// </summary>
static Dictionary<string, List<string>> GetJsonIgnoredPropertiesMap()
{
    var result = new Dictionary<string, List<string>>();

    // Types that are exported via TypeGen and may have [JsonIgnore] or static properties
    var typesToCheck = new Type[]
    {
        typeof(HexCoordinates),
        // Add other types here as needed
    };

    foreach (var type in typesToCheck)
    {
        var ignoredProps = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            // Exclude properties marked with [JsonIgnore]
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
            {
                ignoredProps.Add(prop.Name);
                continue;
            }

            // Exclude static properties (they're never part of instance serialization)
            var getter = prop.GetGetMethod();
            if (getter != null && getter.IsStatic)
            {
                ignoredProps.Add(prop.Name);
            }
        }

        if (ignoredProps.Count > 0)
        {
            result[type.Name] = ignoredProps;
        }
    }

    return result;
}

/// <summary>
/// Removes import statements for types that are no longer referenced in the file content.
/// </summary>
static string RemoveUnusedImports(string content)
{
    // Find all import lines (handle both Unix \n and Windows \r\n line endings)
    var importPattern = @"^import \{ (\w+) \} from '\.\/[\w-]+';";
    var matches = Regex.Matches(content, importPattern, RegexOptions.Multiline);

    foreach (Match match in matches)
    {
        var importedType = match.Groups[1].Value;
        var importLine = match.Value;

        // Check if the type is used elsewhere in the file (not in the import line itself)
        var contentWithoutImport = content.Replace(importLine, "");

        // Look for the type being used (as a type annotation, not just in a string)
        var typeUsagePattern = $@"\b{importedType}\b";
        if (!Regex.IsMatch(contentWithoutImport, typeUsagePattern))
        {
            // Type is not used, remove the import line using regex
            // This handles: trailing whitespace, optional \r\n or \n, and last line without newline
            var linePattern = Regex.Escape(importLine) + @"\s*\r?\n?";
            content = Regex.Replace(content, linePattern, "");
        }
    }

    return content;
}

// ============================================================================
// Utility Functions
// ============================================================================

/// <summary>
/// Converts PascalCase to kebab-case for TypeScript file names.
/// </summary>
static string ToKebabCase(string pascalCase)
{
    if (string.IsNullOrEmpty(pascalCase))
        return pascalCase;

    var sb = new StringBuilder();
    for (int i = 0; i < pascalCase.Length; i++)
    {
        var c = pascalCase[i];
        if (char.IsUpper(c))
        {
            if (i > 0)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        else
        {
            sb.Append(c);
        }
    }
    return sb.ToString();
}

/// <summary>
/// Escapes a string for use in TypeScript single-quoted string.
/// </summary>
static string EscapeString(string value)
{
    return value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r");
}
