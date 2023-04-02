using Lst.Model;
using Microsoft.Win32;

namespace Lst.WindowsService;

#pragma warning disable CA1416

public class Worker : BackgroundService
{
    private const string TimeZoneId = "Local Sun Time";
    private const string TimeZonesRegistryHive = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones";
    private const string TimeZoneKeyName = TimeZonesRegistryHive + @"\" + TimeZoneId;
    private const string OptionHashCodeKeyName = "State_OptionsHashCode";
    private const string OffsetMinutesKeyName = "State_OffsetMinutes";
    private const string VersionKeyName = "State_Version";

    private const int Version = 1;

    private readonly ILogger<Worker> _logger;

    private static readonly TimeZoneOptions TimeZoneOptions = new(
        new Geolocation(-43, 172),
        OffsetResolution.FifteenMinutes,
        TimeSpan.FromHours(-4),
        TimeSpan.Zero);

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var rules = TimeZoneRuleModule.calculateRules(TimeZoneOptions);
            var adjustment = TimeZoneAdjustmentModule.calculateAdjustments(rules, DateTimeOffset.UtcNow).First();

            if (ShouldUpdate(ReadStateFromRegistry(), TimeZoneOptions, adjustment))
            {
                _logger.LogInformation("Updating timezone in registry");
                CreateTimezone(TimeZoneOptions, adjustment);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private static State? ReadStateFromRegistry()
    {
        using var tzKey = Registry.LocalMachine.OpenSubKey(TimeZoneKeyName);
        if (tzKey is null)
        {
            return null;
        }

        var optionsHashCode = (string?)tzKey.GetValue(OptionHashCodeKeyName);
        var offsetMinutes = (string?)tzKey.GetValue(OffsetMinutesKeyName);
        var version = (string?)tzKey.GetValue(VersionKeyName);

        if (string.IsNullOrWhiteSpace(optionsHashCode) ||
            string.IsNullOrWhiteSpace(offsetMinutes) ||
            string.IsNullOrWhiteSpace(version))
        {
            // If any of the expected values are null, just return null (i.e. the state is invalid, blow everything away)
            return null;
        }

        return
            !int.TryParse(offsetMinutes, out var offsetMinutesValue) ||
            !int.TryParse(version, out var versionValue)
                ? null
                : new State(optionsHashCode, offsetMinutesValue, versionValue);
    }

    private static bool ShouldUpdate(State? state, TimeZoneOptions options, TimeZoneAdjustment adjustment)
    {
        if (state is null)
        {
            return true;
        }

        var optionsHashCode = options.GetHashCode().ToString();
        var offsetMinutes = (int)adjustment.Offset.TotalMinutes;
        return
            state.OptionsHashCode != optionsHashCode ||
            state.OffsetMinutes != offsetMinutes ||
            state.Version != Version;
    }

    private static void CreateTimezone(TimeZoneOptions timeZoneOptions, TimeZoneAdjustment adjustment)
    {
        const string name = "Local Sun Time";

        var sign = adjustment.Offset < TimeSpan.Zero ? "-" : "+";
        var displayName = $"(UTC{sign}{adjustment.Offset:hh\\:mm}) Local Sun Time";

        var offsetMinutes = (int)adjustment.Offset.TotalMinutes;
        var tzi = new Structs.REG_TZI_FORMAT(
            Bias: -offsetMinutes,
            StandardBias: 0,
            DaylightBias: 0,
            StandardDate: Structs.SYSTEM_TIME.Empty,
            DaylightDate: Structs.SYSTEM_TIME.Empty
        );

        using var key = RecreateKey(TimeZoneKeyName);

        key.SetValue("Display", displayName, RegistryValueKind.String);
        key.SetValue("MUI_Display", displayName, RegistryValueKind.String);

        key.SetValue("Dlt", name, RegistryValueKind.String);
        key.SetValue("MUI_Dlt", name, RegistryValueKind.String);

        key.SetValue("Std", name, RegistryValueKind.String);
        key.SetValue("MUI_Std", name, RegistryValueKind.String);

        key.SetValue("TZI", StructTools.RawSerialize(tzi), RegistryValueKind.Binary);

        key.SetValue(OptionHashCodeKeyName, timeZoneOptions.GetHashCode().ToString(), RegistryValueKind.String);
        key.SetValue(OffsetMinutesKeyName, offsetMinutes.ToString(), RegistryValueKind.String);
        key.SetValue(VersionKeyName, Version.ToString(), RegistryValueKind.String);
    }

    private static RegistryKey RecreateKey(string name)
    {
        Registry.LocalMachine.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        return Registry.LocalMachine.CreateSubKey(name, writable: true);
    }

    private record State(string OptionsHashCode, int OffsetMinutes, int Version);
}