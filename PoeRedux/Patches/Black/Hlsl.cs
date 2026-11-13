using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches.Black;

public class Hlsl : IPatch
{
    public string Name => "Hlsl Patch";
    public string Description => "";

    private static readonly string[] Targets =
    [
        "shaders/postprocessuber.hlsl",
        "shaders/bloomcutoff.hlsl",
        "shaders/bloomgather.hlsl",
        "shaders/blur.hlsl",
        "shaders/copytexture.hlsl",
        "shaders/depthawareblur.hlsl",
        "shaders/gaussianblur.hlsl",
        "shaders/postprocessoutput.hlsl",
        "shaders/screenspaceshadows.hlsl",
        "shaders/include/lighting.hlsl",
        "shaders/include/projection.hlsl",
    ];

    // Return type, name and parameters of a function.
    private const string FunctionPattern = @"(float|float2|float3|float4|float2x2|float3x3|float4x4|void|OutPixel)\s+\w+\s*\([^)]*\)";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".hlsl"))
        {
            if (Targets.Contains(file.Record.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase))
                plan.Add(file, bytes => Encoding.UTF8.GetBytes(StubFunctions(Encoding.UTF8.GetString(bytes))));
        }
    }

    // Replaces the body of each function, at most 100 of them, with one that returns zero.
    private static string StubFunctions(string text)
    {
        var done = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var function = Regex.Matches(text, FunctionPattern).FirstOrDefault(m => !done.Contains(m.Value));
            if (function is null)
                break;

            done.Add(function.Value);
            text = Braces.ReplaceBlock(text, function.Index, Stub(function.Groups[1].Value));
        }
        return text;
    }

    private static string Stub(string returnType) => returnType switch
    {
        "void" => "{ }",
        "OutPixel" => "{\tOutPixel res;\r\n\tres.shadowmap_data = float4(1.0f, 1.0f, 1.0f, 1.0f);\r\n\treturn res;\n}",
        _ => $"{{\n\treturn ({returnType})0;\n}}",
    };
}
