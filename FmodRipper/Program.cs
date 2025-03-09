using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using System.Text;

string path;
if (args.Length != 1 || !File.Exists(path = args[0]) || !path.EndsWith(".bank"))
{
    Console.WriteLine("Please provide the path to an FMOD bank file.");
    return;
}

string root = Path.GetDirectoryName(path)!;
string name = Path.GetFileNameWithoutExtension(path);
string ripDir = Path.Combine(root, name);
Directory.CreateDirectory(ripDir);

// asset ripping code below is heavily inspired by FenixProFmod under the MIT license.
// https://github.com/M0n7y5/FenixProFmod/blob/master/FenixProFmodAva/ViewModels/MainViewModel.cs

byte[]? bankData = await TryReadFsb(path);

if (bankData == null)
{
    Console.WriteLine("Unable to read FSB data");
    return;
}

if (FsbLoader.TryLoadFsbFromByteArray(bankData, out FmodSoundBank? bank))
{
    foreach (FmodSample sample in bank!.Samples)
    {
        if (sample.RebuildAsStandardFileFormat(out byte[]? sampleData, out string? fileExtension))
        {
            string sampleFileName = $"{sample.Name}.{fileExtension}";
            string samplePath = Path.Combine(ripDir, sampleFileName);
            Console.WriteLine($"Loaded {sampleFileName}");
            try
            {
                using FileStream sampleFile = File.Create(samplePath);
                await sampleFile.WriteAsync(sampleData);
                Console.WriteLine($"Successfully ripped to {samplePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to rip due to an unexpected error: {ex}");
            }
        }
        else
        {
            Console.WriteLine($"Could not reconstruct as standard file format for {sample.Name}");
        }
    }
}
else
{
    Console.WriteLine("Failed to load bank");
}

static int Search(byte[] src, byte[] pattern)
{
    int maxStart = src.Length - pattern.Length + 1;
    for (int i = 0; i < maxStart; i++)
    {
        if (src[i] == pattern[0])
        {
            for (int j = 1; j < pattern.Length; j++)
            {
                if (src[i + j] != pattern[j])
                {
                    break;
                }
                if (j == pattern.Length - 1)
                {
                    return i;
                }
            }
        }
    }
    return -1;
}

static async Task<byte[]?> TryReadFsb(string filePath)
{
    using Stream fs = File.OpenRead(filePath);
    using MemoryStream bankStream = new();
    await fs.CopyToAsync(bankStream);

    bankStream.Position = 0;
    int headerIdx = Search(bankStream.GetBuffer(), Encoding.ASCII.GetBytes("SNDH"));
    bankStream.Position = 0;

    if (headerIdx == -1)
    {
        return null;
    }

    BinaryReader reader = new(bankStream);
    MemoryStream fsbStream = new();
    bankStream.Position = headerIdx + 12;
    int nextOffset = reader.ReadInt32();
    reader.ReadInt32();
    bankStream.Position = nextOffset;
    await bankStream.CopyToAsync(fsbStream);

    return fsbStream.ToArray();
}