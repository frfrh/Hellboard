// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;

namespace HellboardCompanion.Core.Hellboard;

/// <summary>
/// In-memory Hellboard that implements the protocol like the firmware should
/// (hellboard-protocol.md): for tests and for using the Companion without a board.
/// </summary>
public sealed class SimulatedHellboard : IHellboardConnection
{
    public const string FirmwareVersion = "0.0.0-simulator";

    private readonly Dictionary<int, string> _answered = new();
    private JsonNode? _stratagems = new JsonArray(null, null, null, null);
    private JsonNode? _missionStratagems = new JsonArray();
    private JsonObject _config = new()
    {
        ["fixed"] = new JsonObject
        {
            ["1_1"] = new JsonObject { ["id"] = "reinforce", ["name"] = "Reinforce", ["color"] = "yellow", ["sequence"] = "UDRLU" },
            ["1_2"] = new JsonObject { ["id"] = "resupply", ["name"] = "Resupply", ["color"] = "yellow", ["sequence"] = "DDUR" },
        },
        ["modifierKey"] = 6, ["modifierToggle"] = false, ["keyDelayMs"] = 50, ["directionKeys"] = "arrows", ["brightness"] = 80, ["logLevel"] = "info",
    };

    private JsonObject _savedConfig = new();
    private readonly List<JsonObject> _log = [new JsonObject { ["level"] = "info", ["t"] = 10, ["msg"] = "Simulator started" }];

    private string _source = "builtin";

    public string Description => "Simulator";

    /// <summary>Last loadout received with <c>setLoadout</c> (protocol JSON).</summary>
    public JsonNode? Stratagems => _stratagems;

    public JsonNode? MissionStratagems => _missionStratagems;

    /// <summary>Content written by <c>saveDefault</c>, i.e. the simulated flash file.</summary>
    public string? SavedDefault { get; private set; }

    public int SaveCount { get; private set; }

    /// <summary>Number of <c>saveConfig</c> requests (flash writes of the configuration).</summary>
    public int SaveConfigCount { get; private set; }

    /// <summary>Current configuration (protocol §4.4).</summary>
    public JsonObject Config => _config;

    /// <summary>Number of accepted <c>setLoadout</c> requests (repeated ids not counted).</summary>
    public int SetLoadoutCount { get; private set; }

    /// <summary>When true, the simulator does not answer (unplugged / old firmware).</summary>
    public bool Silent { get; set; }

    public event EventHandler<string>? LineReceived;

    public event EventHandler? Closed;

    public void WriteLine(string line)
    {
        if (Silent)
        {
            return;
        }

        var answer = Handle(line);
        if (answer is not null)
        {
            ThreadPool.QueueUserWorkItem(_ => LineReceived?.Invoke(this, answer));
        }
    }

    /// <summary>Simulates a restart of the board (sends the <c>hello</c> event).</summary>
    public void Restart()
    {
        _answered.Clear();
        _source = SavedDefault is null ? "builtin" : "default";
        LineReceived?.Invoke(this, $"{{\"v\":1,\"event\":\"hello\",\"fw\":\"{FirmwareVersion}\",\"protocol\":1}}");
    }

    public void Unplug() => Closed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }

    private string? Handle(string line)
    {
        JsonObject request;
        try
        {
            request = JsonNode.Parse(line) as JsonObject ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return Error(null, "invalid_json", "not a JSON object");
        }

        var id = request["id"]?.GetValue<int>();
        if (id is { } known && _answered.TryGetValue(known, out var repeated))
        {
            return repeated;   // idempotent: same id → same answer
        }

        var answer = request["v"]?.GetValue<int>() != 1
            ? Error(id, "unsupported_version", "v must be 1")
            : request["cmd"]?.GetValue<string>() switch
            {
                "ping" => Ok(id, new JsonObject { ["board"] = "hellboard", ["fw"] = FirmwareVersion, ["protocol"] = 1, ["micropython"] = "simulator" }),
                "setLoadout" => SetLoadout(id, request),
                "getState" => Ok(id, new JsonObject
                {
                    ["stratagems"] = _stratagems?.DeepClone(),
                    ["missionStratagems"] = _missionStratagems?.DeepClone(),
                    ["config"] = _config.DeepClone(),
                    ["configSaved"] = ConfigSaved,
                    ["source"] = _source,
                }),
                "setConfig" => SetConfig(id, request),
                "saveDefault" => SaveDefault(id),
                "saveConfig" => SaveConfig(id),
                "getLog" => Ok(id, new JsonObject { ["entries"] = new JsonArray([.. _log.Select(e => (JsonNode)e.DeepClone())]) }),
                "enterUpdateMode" => Ok(id, []),
                _ => Error(id, "unknown_command", request["cmd"]?.ToString()),
            };

        if (id is { } answeredId)
        {
            _answered[answeredId] = answer;
        }

        return answer;
    }

    private string SetLoadout(int? id, JsonObject request)
    {
        if (request["stratagems"] is not JsonArray stratagems || stratagems.Count != 4)
        {
            return Error(id, "invalid_loadout", "stratagems must have 4 entries");
        }

        if (request["missionStratagems"] is not JsonArray mission || mission.Count > 16)
        {
            return Error(id, "invalid_loadout", "missionStratagems must have 0-16 entries");
        }

        foreach (var entry in stratagems.Concat(mission).OfType<JsonObject>())
        {
            var name = entry["name"]?.GetValue<string>();
            var sequence = entry["sequence"]?.GetValue<string>();
            if (string.IsNullOrEmpty(name) || name.Length > 64 || sequence is null || sequence.Length is < 1 or > 10 || sequence.Any(c => "UDLR".IndexOf(c) < 0))
            {
                return Error(id, "invalid_loadout", $"invalid stratagem '{name}'");
            }
        }

        SetLoadoutCount++;
        _stratagems = stratagems.DeepClone();
        _missionStratagems = mission.DeepClone();
        _source = "pc";
        return Ok(id, []);
    }

    private string SetConfig(int? id, JsonObject request)
    {
        if (request["config"] is not JsonObject changes)
        {
            return Error(id, "invalid_config", "config missing");
        }

        foreach (var (key, value) in changes)
        {
            _config[key] = value?.DeepClone();
        }

        _log.Add(new JsonObject { ["level"] = "info", ["t"] = Environment.TickCount64, ["msg"] = "Configuration changed" });

        return Ok(id, new JsonObject { ["config"] = _config.DeepClone(), ["configSaved"] = ConfigSaved });
    }

    private bool ConfigSaved => JsonNode.DeepEquals(_config, _savedConfig);

    private string SaveDefault(int? id)
    {
        SavedDefault = new JsonObject
        {
            ["stratagems"] = _stratagems?.DeepClone(),
            ["missionStratagems"] = _missionStratagems?.DeepClone(),
        }.ToJsonString();
        SaveCount++;
        return Ok(id, []);
    }

    private string SaveConfig(int? id)
    {
        _savedConfig = (JsonObject)_config.DeepClone();
        SaveConfigCount++;
        return Ok(id, new JsonObject { ["configSaved"] = true });
    }

    /// <summary>Simulates a firmware log entry (event <c>log</c>, F12).</summary>
    public void Log(string level, string message)
    {
        var entry = new JsonObject { ["level"] = level, ["t"] = Environment.TickCount64, ["msg"] = message };
        _log.Add(entry);
        var line = new JsonObject { ["v"] = 1, ["event"] = "log", ["level"] = level, ["t"] = entry["t"]!.GetValue<long>(), ["msg"] = message };
        LineReceived?.Invoke(this, line.ToJsonString());
    }

    private static string Ok(int? id, JsonObject result)
    {
        result["v"] = 1;
        result["ack"] = id;
        result["ok"] = true;
        return result.ToJsonString();
    }

    private static string Error(int? id, string code, string? message) =>
        new JsonObject { ["v"] = 1, ["ack"] = id, ["ok"] = false, ["error"] = code, ["message"] = message }.ToJsonString();
}
