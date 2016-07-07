using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace UnityPacker
{
    internal static class Program
    {
        private static readonly Random _random = new Random();

        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("UnityPacker {Source Path} {Package Name = \"Package\"} {Root Path = \"\"} {Skipped Extensions (CSV) = \"\"} {Skipped Directories (CSV) = \"\"}");
                return;
            }
            
            var inpath = args[0];
            var fileName = args.Length > 1 ? args[1] : "Package";
            var meaningfulHashes = args.Length > 2 && (args[2].ToLower() == "y" || args[2].ToLower() == "yes");
            var root = args.Length > 3 ? args[3] : "";
            var exts = args.Length > 4 ? args[4].Split(',') : new string[0];
            var dirs = args.Length > 5 ? args[5].Split(',') : new string[0];

            var extensions = new List<string>(exts)
            {
                "meta"
            };
            
            var files = Directory.GetFiles(inpath, "*.*", SearchOption.AllDirectories);

            var tmpPath = Path.Combine(Path.GetTempPath(), "packUnity" + RandomStuff(8));
            Directory.CreateDirectory(tmpPath);

			for	(var i = 0; i < files.Length; ++i)
			{
				var file = files[i];
				var sI = file.IndexOf("Assets", StringComparison.Ordinal); // HACK
				var altName = file.Substring(sI+7);
				if (file.StartsWith("."))
                	altName = altName.Replace($".{Path.DirectorySeparatorChar}", "");
				
				var skip = false;
                foreach (var dir in dirs)
                {
                    if (altName.StartsWith(dir))
                    {
                        skip = true;
                        break;
                    }
                }

                var extension = Path.GetExtension(file).Replace(".", "");
                if (skip || extensions.Contains(extension))
                    continue;
                
                string hash1 = RandomHash(), hash2 = RandomHash();

                var metaFile = file + ".meta";                    
                if (meaningfulHashes && File.Exists(metaFile))
                {
                    var hash = "";
                    
                    using (var read = new StreamReader(metaFile))
                    {
                        while (!read.EndOfStream)
                        {
                            var line = read.ReadLine();
                            if (line == null || !line.StartsWith("guid")) continue;
                            hash = line.Split(' ')[1];
                            break;
                        }
                    }
                    hash1 = hash;
                }

                var path = Path.Combine(tmpPath, hash1);
                Directory.CreateDirectory(path);

                File.Copy(file, Path.Combine(path, "asset"));
                if (meaningfulHashes) File.Copy(metaFile, Path.Combine(path, "asset.meta"));
			    using (var writer = new StreamWriter(Path.Combine(path, "pathname")))
			    {
			        writer.Write($"{root}{altName.Replace(Path.DirectorySeparatorChar + "", "/")}\n{hash2}");
			    }
            }

            CreateTarGz($"{fileName}.unitypackage", tmpPath);

            Directory.Delete(tmpPath, true);
        }
        private static string RandomHash()
        {
            return CreateMd5(RandomStuff()).ToLower();
        }
        public static string CreateMd5(string input)
        {
            var md5 = MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            for (var i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }        
        private static string RandomStuff(int len = 32)
        {
            var c = "";
            for (var i = 0; i < len; i++)
            {
                c += _random.Next(0, 128);
            }
            return c;
        }
        private static void CreateTarGz(string tgzFilename, string sourceDirectory)
        {
            var outStream = File.Create(tgzFilename);
            var gzoStream = new GZipOutputStream(outStream);
            var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream);
            
			tarArchive.RootPath = sourceDirectory.Replace('\\', '/');
            if (tarArchive.RootPath.EndsWith("/"))
            {
                tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);
            }            

            AddDirectoryFilesToTar(tarArchive, sourceDirectory, true);

            tarArchive.Close();
        }

        private static void AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse)
        {
            var filenames = Directory.GetFiles(sourceDirectory);
            foreach (var filename in filenames)
            {
                var tarEntry = TarEntry.CreateEntryFromFile(filename);
                tarEntry.TarHeader.UserName = string.Empty;
                tarEntry.TarHeader.GroupName = string.Empty;
                tarEntry.TarHeader.UserId = 0;
                tarEntry.TarHeader.Mode = 33261;
                tarEntry.Name = filename.Remove(0, tarArchive.RootPath.Length + 1);
                tarEntry.Name = $"./{tarEntry.Name}";                
                tarArchive.WriteEntry(tarEntry, true);
            }

            if (!recurse) return;

            var directories = Directory.GetDirectories(sourceDirectory);
            foreach (var directory in directories)
            {
                AddDirectoryFilesToTar(tarArchive, directory, true);
            }
        }
    }
}
