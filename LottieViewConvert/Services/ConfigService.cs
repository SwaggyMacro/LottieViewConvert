using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LottieViewConvert.Models;

namespace LottieViewConvert.Services;

public class ConfigService
{
    public readonly string ConfigFilePath;
    private AppConfig? _config;

    public ConfigService()
    {
        var appDataPath = Path.Combine(AppContext.BaseDirectory, "Configuration");
            
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }
            
        ConfigFilePath = Path.Combine(appDataPath, "config.json");
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        if (_config != null)
            return _config;

        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = await File.ReadAllTextAsync(ConfigFilePath);
                _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                _config = new AppConfig();
                await SaveConfigAsync(_config);
            }
        }
        catch (Exception)
        {
            _config = new AppConfig();
        }

        return _config;
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            await File.WriteAllTextAsync(ConfigFilePath, json);
            _config = config;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save configuration", ex);
        }
    }
    
    public AppConfig LoadConfig()
    {
        if (_config != null)
            return _config;

        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                _config = new AppConfig();
                SaveConfig(_config);
            }
        }
        catch (Exception)
        {
            _config = new AppConfig();
        }

        return _config;
    }

    public void SaveConfig(AppConfig config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
            _config = config;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save configuration", ex);
        }
    }

    public AppConfig GetConfig()
    {
        return _config ?? new AppConfig();
    }
}