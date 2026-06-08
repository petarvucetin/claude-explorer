using ClaudeExplorer.Core.Dependencies;
using ClaudeExplorer.Core.Mcp;

namespace ClaudeExplorer.App.Screens.Mcp;

/// <summary>Health of a server's runtime, for the pill on the MCP screen.</summary>
public enum McpHealth { Ok, Missing, Unverifiable, Na }

public sealed record McpRow(
    string Name,
    McpTransport Transport,
    string Endpoint,
    string SourceLabel,
    McpHealth Health,
    string? Runtime,
    string? Command,
    IReadOnlyList<string> Args,
    string? Url,
    IReadOnlyDictionary<string, string> Env,
    string SourceFile);

public sealed record McpView(IReadOnlyList<McpRow> Rows, int Total, int Stdio, int Remote, int Missing);

/// <summary>Pure mapper: joins discovered MCP servers with the dependency-health report. Stdio servers
/// get a health pill from their runtime's status; http/sse are remote → not applicable.</summary>
public static class McpRowsMapper
{
    public static McpView Map(IReadOnlyList<McpServerInfo> servers, DependencyReport health)
    {
        var rows = servers.Select(s =>
        {
            var runtime = s.Transport == McpTransport.Stdio ? ExecutableExtractor.Extract(s.Command) : null;
            var status = runtime is null
                ? (McpHealth?)null
                : Health(health, runtime);
            return new McpRow(
                s.Name, s.Transport, s.Endpoint, s.SourceLabel,
                s.Transport == McpTransport.Stdio ? status ?? McpHealth.Unverifiable : McpHealth.Na,
                runtime, s.Command, s.Args, s.Url, s.Env, s.SourceFile);
        }).ToList();

        return new McpView(
            rows,
            rows.Count,
            rows.Count(r => r.Transport == McpTransport.Stdio),
            rows.Count(r => r.Transport != McpTransport.Stdio),
            rows.Count(r => r.Health == McpHealth.Missing));
    }

    private static McpHealth Health(DependencyReport report, string runtime)
    {
        var result = report.Results.FirstOrDefault(
            r => string.Equals(r.Ref.Name, runtime, StringComparison.OrdinalIgnoreCase));
        return result?.Status.Kind switch
        {
            DependencyStatusKind.Found => McpHealth.Ok,
            DependencyStatusKind.Missing => McpHealth.Missing,
            DependencyStatusKind.Unverifiable => McpHealth.Unverifiable,
            _ => McpHealth.Unverifiable,
        };
    }

    public static string Pill(McpHealth h) => h switch
    {
        McpHealth.Ok => "ok",
        McpHealth.Missing => "bad",
        McpHealth.Na => "na",
        _ => "warn",
    };
}
