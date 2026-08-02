using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Scrapers.Tests;

/// <summary>
/// Structural guard (schema-guard style): production services must never call
/// <c>System.Diagnostics.Debug</c> members — <c>Debug.WriteLine</c> is a no-op in
/// Release builds, so those logs silently vanish from production (issue #357).
/// Replace any such calls with <c>ILogger</c> instead.
/// </summary>
[TestClass]
public sealed class NoDebugWriteLineGuardTests
{
    [TestMethod]
    public void IngestionApp_HasNoSystemDiagnosticsDebugCalls()
    {
        var hits = FindDebugMemberCalls(typeof(IngestionApp.EventProcessingService).Assembly);
        Assert.AreEqual(0, hits.Count, "IngestionApp calls System.Diagnostics.Debug:\n" + string.Join("\n", hits));
    }

    [TestMethod]
    public void Scrapers_HasNoSystemDiagnosticsDebugCalls()
    {
        var hits = FindDebugMemberCalls(typeof(Scrapers.ClinicalTrialsGov).Assembly);
        Assert.AreEqual(0, hits.Count, "Scrapers calls System.Diagnostics.Debug:\n" + string.Join("\n", hits));
    }

    private static List<string> FindDebugMemberCalls(Assembly assembly)
    {
        var hits = new List<string>();
        var debugType = typeof(System.Diagnostics.Debug);

        foreach (var type in assembly.GetTypes())
        {
            var methods = new List<MethodBase>();
            methods.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            methods.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

            foreach (var method in methods)
            {
                var body = method.GetMethodBody();
                var il = body?.GetILAsByteArray();
                if (il == null)
                {
                    continue;
                }

                var module = method.Module;
                for (var i = 0; i < il.Length; i++)
                {
                    // Opcodes with a following 4-byte metadata token: call (0x28), callvirt (0x6F), newobj (0x73).
                    if (il[i] is not (0x28 or 0x6F or 0x73) || i + 4 >= il.Length)
                    {
                        continue;
                    }

                    var token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var resolved = module.ResolveMethod(token);
                        if (resolved?.DeclaringType == debugType)
                        {
                            hits.Add($"{type.FullName}.{method.Name} -> {resolved}");
                        }
                    }
                    catch (Exception)
                    {
                        // Token resolves to a field/type/other — not a Debug call.
                    }

                    i += 4;
                }
            }
        }

        return hits;
    }
}
