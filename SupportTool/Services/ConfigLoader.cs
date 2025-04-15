using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
namespace SupportTool.Services
{
    public static class ConfigLoader
    {
        public static T LoadConfig<T>(string outputPath, string resourceName)
        {
            if (File.Exists(outputPath))
            {
                string json = File.ReadAllText(outputPath);
                return JsonSerializer.Deserialize<T>(json);
            }

            // Jeśli nie ma pliku - ładujemy domyślny z zasobów
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            string defaultJson = reader.ReadToEnd();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, defaultJson);

            return JsonSerializer.Deserialize<T>(defaultJson);
        }
    }
}
