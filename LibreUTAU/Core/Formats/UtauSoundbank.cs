﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using LibreUtau.Core.USTx;
using LibreUtau.Core.Util;
using LibreUtau.SimpleHelpers;
using Serilog;

namespace LibreUtau.Core.Formats {
    public static class UtauSoundbank {
        private static readonly Dictionary<string, USinger> allSingers = new Dictionary<string, USinger>();

        public static void FindAllSingers() {
            var singerSearchPaths = Preferences.GetSingerSearchPaths();
            foreach (string searchPath in singerSearchPaths) {
                if (!Directory.Exists(searchPath)) continue;
                foreach (var dirpath in Directory.EnumerateDirectories(searchPath)) {
                    USinger singer = null;
                    try {
                        singer = LoadSinger(dirpath);
                        if (singer == null) {
                            Debug.WriteLine($"Error: Unable to load singer from '{dirpath}'");
                        } else allSingers.Add(singer.Path, singer);
                    } catch (Exception e) { throw e; }
                }
            }
        }

        public static Dictionary<string, USinger> GetAllSingers() => allSingers;

        public static USinger GetSinger(string path, Encoding ustEncoding) {
            var absPath = DetectSingerPath(path, ustEncoding);
            if (absPath == "") return null;
            if (allSingers.ContainsKey(absPath)) return allSingers[absPath];

            var singer = LoadSinger(absPath);
            if (singer == null) {
                Debug.WriteLine($"Error: Unable to load singer from '{absPath}'");
            } else allSingers.Add(singer.Path, singer);

            return singer;
        }

        static string DetectSingerPath(string path, Encoding ustEncoding) {
            var pathEncoding = DetectSingerPathEncoding(path, ustEncoding);
            if (pathEncoding == null) return "";
            return PathManager.Inst.GetSingerAbsPath(FileEncoding.ConvertEncoding(ustEncoding, pathEncoding, path));
        }

        static USinger LoadSinger(string path) {
            if (!Directory.Exists(path) ||
                !File.Exists(Path.Combine(path, "character.txt"))) return null;

            USinger singer = new USinger {Path = path};

            LoadOtos(singer);

            try {
                var characterFile = Path.Combine(singer.Path, "character.txt");
                string[] lines = File.ReadAllLines(characterFile,
                    FileEncoding.DetectFileEncoding(characterFile, Encoding.Default));

                foreach (var line in lines) {
                    // TODO: implement adequate property extraction
                    if (line.StartsWith("name=")) singer.Name = line.Trim().Replace("name=", "");
                    if (line.StartsWith("image=")) {
                        string imageFile = line.Trim().Replace("image=", "");
                        var fileEnc =
                            FileEncoding.DetectFileEncoding(Path.Combine(singer.Path, imageFile), Encoding.Default);
                        var pathEnc = DetectPathEncoding(imageFile, singer.Path, fileEnc);
                        Uri imagepath = new Uri(
                            Path.Combine(singer.Path,
                                FileEncoding.ConvertEncoding(fileEnc, pathEnc, imageFile)),
                            UriKind.RelativeOrAbsolute);
                        singer.Avatar = new BitmapImage(imagepath);
                        singer.Avatar.Freeze();
                    }

                    if (line.StartsWith("author=")) singer.Author = line.Trim().Replace("author=", "");
                    if (line.StartsWith("web=")) singer.Website = line.Trim().Replace("web=", "");
                    if (line.StartsWith("web:")) singer.Website = line.Trim().Replace("web:", "");
                }

                LoadPrefixMap(singer);
            } catch { return null; }

            singer.Loaded = true;

            return singer.AliasMap.Count == 0 ? null : singer;
        }

        static Encoding DetectSingerPathEncoding(string singerPath, Encoding ustEncoding) {
            string[] encodings = {"shift_jis", "gbk", "utf-8"};
            return (from encoding in encodings let path =
                        FileEncoding.ConvertEncoding(ustEncoding, Encoding.GetEncoding(encoding), singerPath)
                    where PathManager.Inst.GetSingerAbsPath(path) != "" select Encoding.GetEncoding(encoding))
                .FirstOrDefault();
        }

        static Encoding DetectPathEncoding(string path, string basePath, Encoding encoding) {
            string[] encodings = {"shift_jis", "gbk", "utf-8"};
            return (from enc in encodings let absPath =
                        Path.Combine(basePath, FileEncoding.ConvertEncoding(encoding, Encoding.GetEncoding(enc), path))
                    where File.Exists(absPath) || Directory.Exists(absPath) select Encoding.GetEncoding(enc))
                .FirstOrDefault();
        }

        static void LoadOtos(USinger singer) {
            string path = singer.Path;
            if (File.Exists(Path.Combine(path, "oto.ini"))) LoadOto(path, path, singer);
            foreach (var dirpath in Directory.EnumerateDirectories(path))
                if (File.Exists(Path.Combine(dirpath, "oto.ini")))
                    LoadOto(dirpath, path, singer);
        }

        static void LoadOto(string dirpath, string path, USinger singer) {
            string otoFile = Path.Combine(dirpath, "oto.ini");
            string relativeDir = dirpath.Replace(path, "");
            while (relativeDir.StartsWith("\\")) relativeDir = relativeDir.Substring(1);
            Encoding fileEncoding = FileEncoding.DetectFileEncoding(otoFile, Encoding.Default);
            string[] lines = File.ReadAllLines(otoFile, fileEncoding);
            List<string> errorLines = new List<string>();
            foreach (var line in lines) {
                var s = line.Split('=');
                if (s.Count() == 2) {
                    string wavfile = s[0];
                    var args = s[1].Split(',');
                    if (singer.AliasMap.ContainsKey(args[0])) continue;
                    try {
                        singer.AliasMap.Add(args[0], new UOto {
                            File = Path.Combine(relativeDir, wavfile),
                            Alias = args[0],
                            Offset = double.Parse(args[1], CultureInfo.InvariantCulture),
                            Consonant = double.Parse(args[2], CultureInfo.InvariantCulture),
                            Cutoff = double.Parse(args[3], CultureInfo.InvariantCulture),
                            Preutter = double.Parse(args[4], CultureInfo.InvariantCulture),
                            Overlap = double.Parse(args[5], CultureInfo.InvariantCulture)
                        });
                    } catch {
                        errorLines.Add(line);
                    }
                }
            }

            if (errorLines.Count > 0)
                Debug.WriteLine(
                    $"Oto file {otoFile} has following errors:\n{string.Join("\n", errorLines.ToArray())}");
        }

        static void LoadPrefixMap(USinger singer) {
            // TODO Review this
            string path = singer.Path;
            if (!File.Exists(Path.Combine(path, "prefix.map"))) {
                return;
            }

            string[] lines;
            try {
                lines = File.ReadAllLines(Path.Combine(path, "prefix.map"));
            } catch {
                Log.Error("Prefix map exists but cannot be opened for read.");
                throw new Exception("Prefix map exists but cannot be opened for read.");
            }

            foreach (string line in lines) {
                var s = line.Trim().Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                if (s.Count() == 2) {
                    string source = s[0];
                    string target = s[1];
                    singer.PitchMap.Add(source, target);
                }
            }
        }
    }
}
