using System;
using System.IO;
using System.Text.Json;
using DINBoard.Models;

namespace DINBoard.Services;

public class LicenseService
{
    private const string LocalActivationEnvVarName = "DINBOARD_ENABLE_LOCAL_LICENSE_ACTIVATION";
    private readonly string _licenseFilePath;
    private readonly bool _isLocalActivationShortcutEnabled;
    private LicenseInfo _currentLicense;

    public LicenseService(bool? enableLocalActivationShortcut = null, string? licenseFilePath = null)
    {
        _isLocalActivationShortcutEnabled = enableLocalActivationShortcut ?? ResolveLocalActivationShortcutDefault();

        var resolvedLicenseFilePath = licenseFilePath ?? GetDefaultLicenseFilePath();
        var appFolder = Path.GetDirectoryName(resolvedLicenseFilePath);
        if (string.IsNullOrWhiteSpace(appFolder))
        {
            throw new InvalidOperationException("Nie można ustalić katalogu pliku licencji.");
        }

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _licenseFilePath = resolvedLicenseFilePath;
        _currentLicense = LoadLicense();
    }

    public LicenseInfo CurrentLicense => _currentLicense;
    public bool IsLocalActivationShortcutEnabled => _isLocalActivationShortcutEnabled;

    private static string GetDefaultLicenseFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "DINBoard", "license.json");
    }

    private static bool ResolveLocalActivationShortcutDefault()
    {
#if DEBUG
        return true;
#else
        var flag = Environment.GetEnvironmentVariable(LocalActivationEnvVarName);
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);
#endif
    }

    private static bool IsBuiltInActivationKey(string key)
    {
        return key == "DIN2026-XDFG" || key == "PRO-VERSION-OK";
    }

    private LicenseInfo LoadLicense()
    {
        if (File.Exists(_licenseFilePath))
        {
            try
            {
                var json = File.ReadAllText(_licenseFilePath);
                var license = JsonSerializer.Deserialize<LicenseInfo>(json);
                if (license != null)
                {
                    return license;
                }
            }
            catch
            {
                // W razie błędu zwracamy nową domyślną
            }
        }
        
        var newLicense = new LicenseInfo();
        SaveLicense(newLicense);
        return newLicense;
    }

    private void SaveLicense(LicenseInfo license)
    {
        try
        {
            var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_licenseFilePath, json);
        }
        catch
        {
            // Ignorujemy błędy zapisu na ten moment
        }
    }

    public bool CanCreateNewProject()
    {
        if (!_currentLicense.IsTrial) return true; // Pełna wersja zawsze może
        
        return _currentLicense.TrialProjectsRemaining > 0;
    }

    public void ConsumeTrialProject()
    {
        if (!_currentLicense.IsTrial) return;

        if (_currentLicense.TrialProjectsRemaining > 0)
        {
            _currentLicense.TrialProjectsRemaining--;
            SaveLicense(_currentLicense);
        }
    }

    public bool ActivateLicense(string key)
    {
        if (!IsBuiltInActivationKey(key))
        {
            return false;
        }

        if (!_isLocalActivationShortcutEnabled)
        {
            return false;
        }

        // Prosty mock aktywacji używany tylko w jawnie włączonym trybie lokalnym.
        _currentLicense.IsTrial = false;
        _currentLicense.LicenseKey = key;
        _currentLicense.RegisteredTo = "Użytkownik Premium";
        _currentLicense.ActivationDate = DateTime.Now;

        SaveLicense(_currentLicense);
        return true;
    }
}
