using System;
using System.IO;
using System.Text.Json;

namespace Barcode_File_Find
{
    public class AppSettings
    {
        public string SearchDirectory { get; set; } = string.Empty;
        public string SearchDirectory2 { get; set; } = string.Empty;
        public int IllustratorStartupTimeoutSeconds { get; set; } = 60;
        public int IllustratorDocumentSettleSeconds { get; set; } = 3;
        public bool RunCuttingMasterAfterOpen { get; set; } = true;
        public string AutomationMode { get; set; } = "UIAutomation";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFile))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"설정 저장 중 오류 발생: {ex.Message}");
            }
        }
    }
}


