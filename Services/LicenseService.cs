using System;
using System.IO;
using System.Text.Json;
using DINBoard.Models;

namespace DINBoard.Services;

public class LicenseService
{
    private readonly string _licenseFilePath;
    private LicenseInfo _currentLicense;

    public LicenseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "DINBoard");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _licenseFilePath = Path.Combine(appFolder, "license.json");
        _currentLicense = LoadLicense();
    }

    public LicenseInfo CurrentLicense => _currentLicense;

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
        // Prosty mock aktywacji. W przyszłości można to sprawdzić algorytmem lub połączyć z API na serwerze
        if (key == "DIN2026-XDFG" || key == "PRO-VERSION-OK")
        {
            _currentLicense.IsTrial = false;
            _currentLicense.LicenseKey = key;
            _currentLicense.RegisteredTo = "Użytkownik Premium";
            _currentLicense.ActivationDate = DateTime.Now;
            
            SaveLicense(_currentLicense);
            return true;
        }

        return false;
    }
}
