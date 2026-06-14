using Spectre.Console;
using System.Diagnostics;
using System.CommandLine;

namespace NUUTop
{
    internal static class NuuTop
    {
        private static readonly Dictionary<string, string> PreviousTemps = new();
        private static readonly Dictionary<string, string?> ZoneCache = new();
        
        private static string? _lastBatteryTemp;
        private static bool _showInvalidTemps;
        
        private static readonly Dictionary<string, string> NuuThermalMap = new()
        {
            ["CPU"] = "mtktscpu",             // CPU
            ["SoC"] = "mtktsAP",              // application processor
            ["Battery"] = "mtktsbattery",     // battery
            ["Charger"] = "mtktscharger",     // charger
            ["PMIC"] = "mtktspmic",           // power management
            ["RF_PA"] = "mtktspa",            // RF power amplifier
            ["Wireless"] = "mtktswmt",        // Wi-Fi bluetooth modem
            ["Modem_MD_PA"] = "mtktsbtsmdpa", // modem power amplifier
            ["Modem_NR_PA"] = "mtktsbtsnrpa"  // modem power amplifier duel sim
        };
        private static readonly string[] BatteryTempPaths =
        [
            "/sys/class/power_supply/battery/temp",
            "/sys/class/power_supply/battery/temp_now",
        ];

        private static async Task<int> Main(string[] args)
        {
            var invalidOption = new Option<bool>("--show-invalid", "-si")
            {
                Description = "Show thermal zones with invalid values. (-127)"
            };
            
            var verboseOption = new Option<bool>("--debug-zones", "-dz")
            {
                Description = "Dump all detected thermal zones."
            };
            
            var rootCommand = new RootCommand
            {
                invalidOption,
                verboseOption
            };
            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                try
                {
                    var invalid = parseResult.GetValue(invalidOption);
                    var verbose = parseResult.GetValue(verboseOption);
                
                    if (invalid)
                    {
                        _showInvalidTemps = true;
                    }
                
                    if (verbose)
                    {
                        DumpThermalZones();
                        return Task.CompletedTask;
                    }
                
                    string device =
                        $"{GetProp("ro.product.manufacturer")} " +
                        $"{GetProp("ro.product.name")} " +
                        $"{GetProp("ro.product.device")}";

                    BuildZoneCache();
                
                    foreach (var name in NuuThermalMap.Keys)
                        PreviousTemps[name] = "";

                    string? previousBattery = null;

                    AnsiConsole.Live(BuildLayout(device, previousBattery)).Start(ctx =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var table = BuildTable(ref previousBattery);

                            var panel = new Panel(table)
                                .Header("[bold cyan]NUUTop[/]")
                                .Border(BoxBorder.Rounded);

                            ctx.UpdateTarget(new Rows(new Markup($"[bold yellow]Device:[/] {device}"), panel));

                            ctx.Refresh();

                            cancellationToken.WaitHandle.WaitOne(1000);
                        }
                    });
                    return Task.CompletedTask;
                    
                }
                catch (Exception exception)
                {
                    return Task.FromException(exception);
                }
                finally
                {
                    AnsiConsole.Cursor.Show();
                }
            });

            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static Rows BuildLayout(string device, string? previousBattery)
        {
            return new Rows(new Markup($"[bold yellow]Device:[/] {device}"), new Panel(BuildTable(ref previousBattery))
                .Header("[bold cyan]NUUTop[/]").Border(BoxBorder.Rounded).Expand());
        }

        private static Table BuildTable(ref string? previousBattery)
        {
            var table = new Table().Border(TableBorder.Rounded).Expand();

            table.AddColumn(new TableColumn("[bold]Sensor[/]")
                .Width(12).NoWrap());
            table.AddColumn(new TableColumn("[bold]Current State[/]")
                .Width(20).NoWrap());
            table.AddColumn(new TableColumn("[bold]Trend[/]")
                .Width(5).NoWrap());
            table.AddColumn(new TableColumn("[bold]Previous[/]")
                .Width(8).NoWrap());

            int battery = ReadInt("/sys/class/power_supply/battery/capacity");
            string batteryStatus = ReadFile("/sys/class/power_supply/battery/status");
            string batteryArrow = ArrowCompute(battery.ToString(), previousBattery);
            
            table.AddRow("[green]Battery[/]", $"[bold]{battery}%[/] ({batteryStatus})", 
                ColorArrow(batteryArrow), previousBattery == null ? "-" : $"{previousBattery}%");

            previousBattery = battery.ToString();

            foreach (var name in NuuThermalMap.Keys)
            {
                string current = ReadTemperature(name);
                string previous = PreviousTemps[name];

                string arrow = string.IsNullOrEmpty(previous) ? "-" : ArrowCompute(current, previous);

                table.AddRow(name, FormatTemp(current), ColorArrow(arrow), previous == "" ? "-" : FormatTemp(previous));

                PreviousTemps[name] = current;
            }

            return table;
        }

        private static string FormatTemp(string value)
        {
            return value == "----"
                ? "[grey]----[/]"
                : $"{value}°C";
        }

        private static string ColorArrow(string arrow)
        {
            return arrow switch
            {
                "↑" => "[red]↑[/]",
                "↓" => "[purple]↓[/]",
                _ => "[grey]-[/]"
            };
        }

        private static void BuildZoneCache()
        {
            if (!Directory.Exists("/sys/class/thermal"))
                return;

            foreach (var name in NuuThermalMap.Keys)
                ZoneCache[name] = FindThermalZoneInternal(name);
        }

        private static string? FindThermalZone(string name)
        {
            return ZoneCache.GetValueOrDefault(name);
        }

        private static string? FindThermalZoneInternal(string name)
        {
            if (!NuuThermalMap.TryGetValue(name, out var wanted))
                return null;

            foreach (var dir in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
            {
                string type = ReadFile(Path.Combine(dir, "type"));

                if (type == wanted)
                    return dir;
            }

            return null;
        }

        private static string ReadTemperature(string name)
        {
            return name == "Battery"
                ? ReadBatteryTemperature()
                : ReadThermalTemperature(name);
        }

        private static string ReadBatteryTemperature()
        {
            foreach (var path in BatteryTempPaths)
            {
                if (!File.Exists(path))
                    continue;

                if (!int.TryParse(ReadFile(path), out var raw))
                    continue;

                if (!TryGetTemperature(raw, out var temp))
                    continue;

                string result = temp.ToString("0.0");

                _lastBatteryTemp = result;

                return result;
            }

            string fallback = ReadThermalTemperature("Battery");

            if (fallback == "----") return _lastBatteryTemp ?? "----";
            _lastBatteryTemp = fallback;
            return fallback;

        }

        private static string ReadThermalTemperature(string name)
        {
            string? zone = FindThermalZone(name);

            if (zone == null)
                return "----";

            string tempPath = Path.Combine(zone, "temp");

            if (!File.Exists(tempPath))
                return "----";

            if (!int.TryParse(ReadFile(tempPath), out int raw))
                return "----";

            return !TryGetTemperature(raw, out double temp)
                ? "----"
                : temp.ToString("0.0");
        }

        private static bool TryGetTemperature(int raw, out double temp)
        {
            temp = 0;

            if (!IsValidRawTemperature(raw))
                return false;

            temp = NormalizeTemperature(raw);

            return true;
        }

        private static bool IsValidRawTemperature(int raw)
        {
            return raw != int.MinValue &&
                   raw != int.MaxValue &&
                   raw is > -1000 and < 200000;
        }

        private static double NormalizeTemperature(int raw)
        {
            return raw switch
            {
                > 10000 or < -10000 => raw / 1000.0,
                > 200 or < -200 => raw / 10.0,
                _ => raw
            };
        }

        private static void DumpThermalZones()
        {
            var table = new Table().Border(TableBorder.Rounded);

            table.AddColumn("Zone");
            table.AddColumn("Type");
            table.AddColumn("Temp");
            table.AddColumn("Mapped");

            foreach (var dir in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
            {
                string zone = Path.GetFileName(dir);

                string type = ReadFile(Path.Combine(dir, "type"));

                string tempPath = Path.Combine(dir, "temp");

                if (!int.TryParse(ReadFile(tempPath), out var raw))
                    continue;

                bool valid = TryGetTemperature(raw, out var temp);

                if (!valid && !_showInvalidTemps)
                    continue;

                temp = NormalizeTemperature(raw);

                string mapped = NuuThermalMap.FirstOrDefault(x => x.Value == type).Key ?? "";

                table.AddRow(zone, type, $"{temp:0.0}°C", mapped);
            }

            AnsiConsole.Write(table);
        }

        private static string ArrowCompute(string current, string? previous)
        {
            if (string.IsNullOrEmpty(previous))
                return "-";

            if (!double.TryParse(current, out var c))
                return "-";

            if (!double.TryParse(previous, out var p))
                return "-";

            return c > p ? "↑" : c < p ? "↓" : "-";
        }

        private static string ReadFile(string path)
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch
            {
                return "";
            }
        }

        private static int ReadInt(string path)
        {
            return int.TryParse(ReadFile(path), out var value)
                ? value : 0;
        }

        private static string GetProp(string property)
        {
            try
            {
                using var p = new Process();

                p.StartInfo.FileName = "getprop";
                p.StartInfo.Arguments = property;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;

                p.Start();

                return p.StandardOutput.ReadToEnd().Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}