using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThroneForge.Discovery;

/// <summary>
/// Stable, machine-readable output for callers that must not parse the human CLI presentation.
/// </summary>
public sealed record RuntimeCompatibilityEvidence(
    string SchemaVersion,
    string GameFingerprint,
    string SelectedExecutableRelativePath,
    string ManagedRuntimeProfile,
    string ExecutableArchitecture,
    string? UnityVersion,
    string SmokeTestReadiness,
    bool LoaderIndicatorsAbsent)
{
    public bool IsReadyForReversibleTest
        => string.Equals(SmokeTestReadiness, nameof(global::ThroneForge.Discovery.SmokeTestReadiness.ReadyForReversibleTest), StringComparison.Ordinal)
            && LoaderIndicatorsAbsent;
}

public static class RuntimeCompatibilityEvidenceContract
{
    public const string SchemaVersion = "throneforge-runtime-compatibility-evidence-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string Serialize(RuntimeCompatibilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(result.SelectedExecutableRelativePath))
        {
            throw new DiscoveryException("Machine-readable runtime evidence requires a selected executable.");
        }

        var indicatorsAbsent = result.LoaderIndicators.All(item => item.Status == LoaderIndicatorStatus.Absent);
        var evidence = new RuntimeCompatibilityEvidence(
            SchemaVersion,
            NormalizeFingerprint(result.BaseFingerprint),
            ValidateRelativePath(result.SelectedExecutableRelativePath),
            result.ManagedRuntimeProfile.ToString(),
            result.ExecutableArchitecture.ToString(),
            string.IsNullOrWhiteSpace(result.UnityVersion) ? null : result.UnityVersion,
            result.SmokeTestReadiness.Status.ToString(),
            indicatorsAbsent);

        return JsonSerializer.Serialize(new PersistedEvidence(
            evidence.SchemaVersion,
            evidence.GameFingerprint,
            evidence.SelectedExecutableRelativePath,
            evidence.ManagedRuntimeProfile,
            evidence.ExecutableArchitecture,
            evidence.UnityVersion,
            evidence.SmokeTestReadiness,
            evidence.LoaderIndicatorsAbsent), JsonOptions);
    }

    public static RuntimeCompatibilityEvidence Parse(string json, string? expectedFingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new DiscoveryException("The machine-readable runtime evidence is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new DiscoveryException("The machine-readable runtime evidence must be a JSON object.");
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!values.TryAdd(property.Name, property.Value))
                {
                    throw new DiscoveryException("The machine-readable runtime evidence contains a duplicate field.");
                }
            }

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema-version", "game-fingerprint", "selected-executable-relative-path",
                "managed-runtime-profile", "executable-architecture", "unity-version",
                "smoke-test-readiness", "loader-indicators-absent"
            };
            if (!values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            {
                throw new DiscoveryException("The machine-readable runtime evidence has missing or unexpected fields.");
            }

            var schema = ReadString(values, "schema-version");
            var fingerprint = NormalizeFingerprint(ReadString(values, "game-fingerprint"));
            var selected = ValidateRelativePath(ReadString(values, "selected-executable-relative-path"));
            var profile = ReadString(values, "managed-runtime-profile");
            var architecture = ReadString(values, "executable-architecture");
            var readiness = ReadString(values, "smoke-test-readiness");
            var unity = values["unity-version"].ValueKind == JsonValueKind.Null
                ? null
                : ReadString(values, "unity-version");
            if (!string.Equals(schema, SchemaVersion, StringComparison.Ordinal)
                || !Enum.TryParse<ManagedRuntimeProfile>(profile, out _)
                || !Enum.TryParse<ExecutableArchitecture>(architecture, out _)
                || !Enum.TryParse<SmokeTestReadiness>(readiness, out _)
                || values["loader-indicators-absent"].ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                throw new DiscoveryException("The machine-readable runtime evidence contains unsupported values.");
            }

            var result = new RuntimeCompatibilityEvidence(
                schema,
                fingerprint,
                selected,
                profile,
                architecture,
                unity,
                readiness,
                values["loader-indicators-absent"].GetBoolean());
            if (expectedFingerprint is not null
                && !result.GameFingerprint.Equals(NormalizeFingerprint(expectedFingerprint), StringComparison.Ordinal))
            {
                throw new DiscoveryException("The machine-readable runtime evidence fingerprint does not match the expected installation.");
            }

            return result;
        }
        catch (DiscoveryException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new DiscoveryException("The machine-readable runtime evidence is malformed.", exception);
        }
    }

    private static string ReadString(Dictionary<string, JsonElement> values, string name)
    {
        if (values[name].ValueKind != JsonValueKind.String)
        {
            throw new DiscoveryException("The machine-readable runtime evidence contains a non-string field.");
        }

        var value = values[name].GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DiscoveryException("The machine-readable runtime evidence contains an empty field.");
        }

        return value;
    }

    private static string NormalizeFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DiscoveryException("The machine-readable runtime evidence fingerprint is invalid.");
        }

        return value.ToLowerInvariant();
    }

    private static string ValidateRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || value.Contains(':')
            || value.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new DiscoveryException("The machine-readable runtime evidence executable path is not a safe relative path.");
        }

        return value.Replace('\\', '/');
    }

    private sealed record PersistedEvidence(
        [property: JsonPropertyName("schema-version")] string SchemaVersion,
        [property: JsonPropertyName("game-fingerprint")] string GameFingerprint,
        [property: JsonPropertyName("selected-executable-relative-path")] string SelectedExecutableRelativePath,
        [property: JsonPropertyName("managed-runtime-profile")] string ManagedRuntimeProfile,
        [property: JsonPropertyName("executable-architecture")] string ExecutableArchitecture,
        [property: JsonPropertyName("unity-version")] string? UnityVersion,
        [property: JsonPropertyName("smoke-test-readiness")] string SmokeTestReadiness,
        [property: JsonPropertyName("loader-indicators-absent")] bool LoaderIndicatorsAbsent);
}
