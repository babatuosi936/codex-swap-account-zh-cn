using System.Text;

namespace CodexProfileOverlay.Core.Services;

public sealed class ActiveProfileStore
{
    private readonly string activeProfileFile;

    public ActiveProfileStore(string activeProfileFile)
    {
        this.activeProfileFile = Path.GetFullPath(activeProfileFile);
    }

    public string? Read()
    {
        if (!File.Exists(activeProfileFile))
        {
            return null;
        }

        string value = File.ReadAllText(activeProfileFile, Encoding.UTF8).Trim();
        return ProfileName.IsValid(value) ? value : null;
    }

    public void Write(string profileName)
    {
        string validName = ProfileName.RequireValid(profileName);
        string directory = Path.GetDirectoryName(activeProfileFile)!;
        Directory.CreateDirectory(directory);
        string temporaryFile = Path.Combine(directory, $".active-profile-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryFile, validName, new UTF8Encoding(false));
        try
        {
            if (File.Exists(activeProfileFile))
            {
                File.Replace(temporaryFile, activeProfileFile, null);
            }
            else
            {
                File.Move(temporaryFile, activeProfileFile);
            }
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}
